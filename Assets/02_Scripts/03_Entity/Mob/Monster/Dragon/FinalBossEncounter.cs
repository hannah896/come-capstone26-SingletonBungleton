using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 최종 보스전 진행 (기획 8.1). 아레나 씬에 1개 두고, 호스트(싱글)만 보스를 띄운다.
///
/// 1페이즈 서리룡 → (사망) → 월식 전환 → 2페이즈 망령룡 → (사망) → 클리어
/// - 전환: 장판을 모두 걷고, 조명이 붉고 어둡게 바뀐 뒤(월식), 서리룡이 쓰러진 자리에서 망령룡이 일어선다
///   (DragonIntroState: 이륙 → 포효 → 착지 충격파, 내내 무적). 쓰러짐부터 교전까지 약 15초가 휴식·재정비 시간이다
/// - 두 드래곤은 별도 몬스터라 애니메이터·콜라이더·스탯이 섞이지 않고, 멀티 복제도 기존 몬스터 방식을 그대로 쓴다
///
/// 아직 없는 것: 체력바 HUD(PhaseChanged/CurrentBoss를 구독해 붙인다), 광폭화 타이머, 인원 보정, 월광 장궁 시작 트리거,
/// 멀티 클라의 월식 조명 (조명은 지금 이 피어에서만 바뀐다).
/// </summary>
public class FinalBossEncounter : MonoBehaviour
{
    public enum EncounterPhase { Waiting, Phase1, Transition, Phase2, Cleared }

    #region Inspector
    [Header("보스")]
    [Tooltip("아레나 중심 (보스 등장 위치). 비우면 이 오브젝트 위치. 보스는 이 오브젝트의 정면(forward)을 바라보며 등장한다")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("1페이즈 서리룡 Addressable 키 (MonsterCatalog에 등록된 키)")]
    [SerializeField] private string phase1Key = "PolarDragon";
    [Tooltip("2페이즈 망령룡 Addressable 키")]
    [SerializeField] private string phase2Key = "UndeadDragon";
    [Tooltip("켜면 씬 시작 시 바로 보스전을 시작한다 (테스트용. 본편은 월광 장궁을 집을 때 StartEncounter 호출)")]
    [SerializeField] private bool startOnAwake = false;

    [Header("월식 전환")]
    [Tooltip("서리룡 사망부터 망령룡 등장 연출 시작까지(초). 사망 클립 2.2초 + 월식 연출")]
    [SerializeField] private float phase2SpawnDelay = 6f;
    [Tooltip("월식으로 어두워지는 조명 (비우면 RenderSettings.sun)")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Color eclipseLightColor = new Color(0.75f, 0.25f, 0.2f, 1f);
    [SerializeField] private float eclipseLightIntensity = 0.35f;
    [SerializeField] private float eclipseAmbientMultiplier = 0.4f;
    [SerializeField] private float eclipseFadeTime = 5f;
    #endregion

    private DragonBoss currentBoss;
    private Color originalLightColor;
    private float originalLightIntensity;
    private float originalAmbient;
    private bool lightCaptured;

    /// <summary>현재 진행 단계.</summary>
    public EncounterPhase Phase { get; private set; } = EncounterPhase.Waiting;
    /// <summary>지금 싸우는 보스 (전환 중에는 null). HUD가 체력·무력화 게이지를 읽는다.</summary>
    public DragonBoss CurrentBoss => currentBoss;

    /// <summary>단계가 바뀔 때 (HUD 체력바 교체 등).</summary>
    public event Action<EncounterPhase> PhaseChanged;
    /// <summary>2페이즈 보스를 잡았을 때 (월식의 눈 드랍·엔딩 연결 지점).</summary>
    public event Action Cleared;

    private Vector3 CenterPosition => arenaCenter != null ? arenaCenter.position : transform.position;
    private Quaternion CenterRotation => arenaCenter != null ? arenaCenter.rotation : transform.rotation;

    private void Start()
    {
        if (startOnAwake) StartEncounter();
    }

    /// <summary>보스전 시작: 아레나 중앙에 서리룡을 띄운다. 호스트(싱글)에서만 동작한다.</summary>
    public void StartEncounter()
    {
        if (Phase != EncounterPhase.Waiting && Phase != EncounterPhase.Cleared) return;
        if (!Monster.IsSimulatedPeer) return;

        CaptureLighting();
        RunPhase1Async(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RunPhase1Async(CancellationToken token)
    {
        var boss = await SpawnBossAsync(phase1Key, CenterPosition, CenterRotation);
        if (token.IsCancellationRequested) return;
        if (boss == null)
        {
            Debug.LogError($"[FinalBossEncounter] 1페이즈 보스 '{phase1Key}' 스폰 실패", this);
            return;
        }

        currentBoss = boss;
        boss.Died += OnPhase1Died;
        SetPhase(EncounterPhase.Phase1);
    }

    private void OnPhase1Died(Monster dead)
    {
        dead.Died -= OnPhase1Died;
        Vector3 pos = dead.transform.position;
        Quaternion rot = dead.transform.rotation;
        currentBoss = null;
        SetPhase(EncounterPhase.Transition);
        RunTransitionAsync(pos, rot, this.GetCancellationTokenOnDestroy()).Forget();
    }

    // 월식 전환: 장판 정리 → 조명이 붉고 어둡게 → 같은 자리에서 망령룡 등장
    private async UniTaskVoid RunTransitionAsync(Vector3 position, Quaternion rotation, CancellationToken token)
    {
        DragonHazardZone.ClearAll();
        FadeLightingAsync(true, token).Forget();

        await UniTask.Delay(TimeSpan.FromSeconds(phase2SpawnDelay), cancellationToken: token).SuppressCancellationThrow();
        if (token.IsCancellationRequested) return;

        position.y = CenterPosition.y; // 공중에서 죽어도 땅에서 일어선다
        var boss = await SpawnBossAsync(phase2Key, position, rotation);
        if (token.IsCancellationRequested) return;
        if (boss == null)
        {
            Debug.LogError($"[FinalBossEncounter] 2페이즈 보스 '{phase2Key}' 스폰 실패", this);
            return;
        }

        currentBoss = boss;
        boss.Died += OnPhase2Died;
        boss.BeginIntro();
        SetPhase(EncounterPhase.Phase2);
    }

    private void OnPhase2Died(Monster dead)
    {
        dead.Died -= OnPhase2Died;
        currentBoss = null;
        FadeLightingAsync(false, this.GetCancellationTokenOnDestroy()).Forget(); // 월식이 걷힌다
        SetPhase(EncounterPhase.Cleared);
        Cleared?.Invoke();
    }

    private void SetPhase(EncounterPhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }

    /// <summary>
    /// 보스를 띄운다. MonsterCatalog에 있으면 그 스탯 SO를 주입하고 멀티에서는 복제 대상으로 등록한다.
    /// 아레나 중심을 넘겨 돌진·활공·발악이 이 원을 기준으로 움직이게 한다.
    /// </summary>
    private async UniTask<DragonBoss> SpawnBossAsync(string key, Vector3 position, Quaternion rotation)
    {
        var catalog = MonsterCatalog.Instance;
        byte catalogId = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
        var entry = catalogId != 0 ? catalog.GetEntry(catalogId) : null;

        var monster = await Monster.SpawnAsync(key, entry?.statData, position);
        if (monster == null) return null;
        monster.transform.rotation = rotation;

        var boss = monster as DragonBoss;
        if (boss == null)
        {
            Debug.LogError($"[FinalBossEncounter] '{key}'는 DragonBoss가 아님", this);
            Extensions.Despawn(monster.gameObject);
            return null;
        }
        boss.SetArenaCenter(CenterPosition);

#if PHOTON_FUSION
        if (catalogId != 0 && Main.Network != null && Main.Network.IsInRoom)
            NetworkMonsterDirector.Instance?.RegisterMonster(boss, catalogId);
#endif
        return boss;
    }

    #region 월식 조명
    private Light Sun => sunLight != null ? sunLight : RenderSettings.sun;

    private void CaptureLighting()
    {
        if (lightCaptured) return;
        lightCaptured = true;
        var sun = Sun;
        if (sun != null)
        {
            originalLightColor = sun.color;
            originalLightIntensity = sun.intensity;
        }
        originalAmbient = RenderSettings.ambientIntensity; // 주변광 모드가 Skybox라 색 대신 세기를 바꾼다
    }

    private async UniTaskVoid FadeLightingAsync(bool toEclipse, CancellationToken token)
    {
        if (!lightCaptured) CaptureLighting();
        var sun = Sun;
        Color fromColor = sun != null ? sun.color : Color.white;
        float fromIntensity = sun != null ? sun.intensity : 1f;
        float fromAmbient = RenderSettings.ambientIntensity;

        Color toColor = toEclipse ? eclipseLightColor : originalLightColor;
        float toIntensity = toEclipse ? eclipseLightIntensity : originalLightIntensity;
        float toAmbient = toEclipse ? originalAmbient * eclipseAmbientMultiplier : originalAmbient;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(eclipseFadeTime, 0.01f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (sun != null)
            {
                sun.color = Color.Lerp(fromColor, toColor, k);
                sun.intensity = Mathf.Lerp(fromIntensity, toIntensity, k);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(fromAmbient, toAmbient, k);
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
        }
    }

    // 씬을 떠날 때 조명을 원래대로 (공유 Light/RenderSettings가 바뀐 채 남지 않게)
    private void OnDestroy()
    {
        if (!lightCaptured) return;
        var sun = Sun;
        if (sun != null)
        {
            sun.color = originalLightColor;
            sun.intensity = originalLightIntensity;
        }
        RenderSettings.ambientIntensity = originalAmbient;
    }
    #endregion
}

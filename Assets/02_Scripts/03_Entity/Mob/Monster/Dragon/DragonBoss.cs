using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>드래곤 지상 패턴 종류. 쿨타임 배열 인덱스로도 쓴다.</summary>
public enum DragonSkill
{
    Bite,   // 서리 물기 ★ (카운터 가능)
    Spit,   // 빙탄 3연사
    Breath, // 서리 브레스
    Charge, // 얼음 돌진
}

/// <summary>
/// 최종 보스 드래곤. 1페이즈 서리룡(Polar Dragon)이 이 클래스를 그대로 쓰고,
/// 2페이즈 망령룡(Undead Dragon)은 스탯 SO·프리팹만 바꾸거나 상속해 전용 기믹을 더한다.
/// (기획: docs/기획_최종보스_월식룡.md)
///
/// - 지상 패턴 4종: 물기★ / 빙탄 3연사 / 브레스 / 돌진 — 선택은 DragonGroundState, 1회 진행은 DragonSkillState·DragonChargeState
/// - 무력화 게이지: 받은 피해(머리는 배율)만큼 깎이고 0이면 그로기 (받는 피해 증가)
/// - 카운터: 물기 선딜 동안 머리가 파랗게 빛난다. 그 사이 머리(정면)를 맞히면 패턴 취소 + 경직 + 무력화 대량 감소
/// - 공중 페이즈: 체력이 TakeOffHpRatios 아래로 내려가면 이륙. 공중에서 받은 피해는 격추 게이지를 깎는다
///   제한 시간 안에 다 깎으면 추락 → 그로기, 못 깎으면 활공 선회 → 착지 → 서리 폭발 전멸기
///
/// 슈퍼아머: 일반 피격 경직·넉백이 없다. 판정·고도·게이지는 이 본체가, 흐름은 상태 클래스가 담당한다.
/// </summary>
public class DragonBoss : Monster
{
    protected override bool ResistsKnockback => true;
    public override bool CanWander => false;

    #region Inspector
    [Header("드래곤 - 애니메이션 Bool 파라미터명 (Dragon 컨트롤러 기준, 없으면 비워둠)")]
    [Tooltip("돌진 (Run). 걷기는 베이스 moveBool, 물기는 attackBool")]
    [SerializeField] private string runBool = "Run";
    [SerializeField] private string spitBool = "Spit";
    [SerializeField] private string breathBool = "Breath";
    [SerializeField] private string hitHeavyBool = "HitHeavy";
    [SerializeField] private string takeOffBool = "TakeOff";
    [SerializeField] private string flyIdleBool = "FlyIdle";
    [SerializeField] private string flySpitBool = "FlySpit";
    [SerializeField] private string flyBreathBool = "FlyBreath";
    [SerializeField] private string fallBool = "Fall";
    [SerializeField] private string landBool = "Land";
    [SerializeField] private string glideBool = "Glide";
    [SerializeField] private string glideLandBool = "GlideLand";
    [SerializeField] private string crashBool = "Crash";

    [Header("드래곤 - 애니메이터 상태 이름 (판정 타이밍을 클립 시작에 맞추는 데 사용)")]
    [SerializeField] private string biteStateName = "BiteAttack";
    [SerializeField] private string spitStateName = "SpitFrozenBall";
    [SerializeField] private string breathStateName = "SpreadFrozenBreath";
    [SerializeField] private string takeOffStateName = "TakeOff";
    [SerializeField] private string flySpitStateName = "FlyStationarySpitFrozenBall";
    [SerializeField] private string flyBreathStateName = "FlyStationarySpreadFrozenBreath";
    [SerializeField] private string landStateName = "FlyStationaryToLanding";
    [SerializeField] private string glideLandStateName = "GlideToLanding";
    [SerializeField] private string crashStateName = "DeathHitTheGround";

    [Header("드래곤 - 뼈 (머리 판정·브레스·탄 발사 위치)")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform neckBone;
    [Tooltip("머리 뼈 기준 입 위치 오프셋(월드 m, 주둥이 방향). 탄·브레스가 여기서 나간다")]
    [SerializeField] private float mouthForwardOffset = 1.0f;
    [SerializeField] private DragonBreathFx breathFx;

    [Header("드래곤 - 이펙트 (Addressable 키)")]
    [SerializeField] private string markerKey = "DragonGroundMarker";
    [SerializeField] private string crashVfxKey = "DragonFrostImpact";
    [SerializeField] private string explosionVfxKey = "DragonFrostExplosion";

    [Header("드래곤 - 효과음 (3D, 비어 있으면 재생 안 함)")]
    [SerializeField] private AudioClip roarSfx;
    [SerializeField] private AudioClip biteSfx;
    [SerializeField] private AudioClip spitSfx;
    [SerializeField] private AudioClip breathSfx;
    [SerializeField] private AudioClip wingSfx;
    [SerializeField] private AudioClip landSfx;
    [SerializeField] private AudioClip crashSfx;
    [SerializeField] private AudioClip counterSfx;
    [SerializeField] private AudioClip explosionSfx;
    [SerializeField] private AudioClip deathSfx;
    #endregion

    // Bool 해시
    private int runBoolHash, spitBoolHash, breathBoolHash, hitHeavyBoolHash;
    private int takeOffBoolHash, flyIdleBoolHash, flySpitBoolHash, flyBreathBoolHash;
    private int fallBoolHash, landBoolHash, glideBoolHash, glideLandBoolHash, crashBoolHash;
    // 상태 해시
    private int biteStateHash, spitStateHash, breathStateHash, takeOffStateHash, flySpitStateHash;
    private int flyBreathStateHash, landStateHash, glideLandStateHash, crashStateHash;

    private static readonly int SkillCount = System.Enum.GetValues(typeof(DragonSkill)).Length;
    private readonly float[] cooldowns = new float[SkillCount];

    private float poise;
    private float poiseRegenWait;
    private float damageMultiplier = 1f;  // 그로기 중 받는 피해 배율
    private bool isGroggy;
    private bool isInvulnerable;

    private bool isAirborne;              // 이륙 ~ 착지(또는 추락) 사이. 체력이 1 아래로 내려가지 않는다
    private float shootdownGauge;
    private int takeOffIndex;
    private bool takeOffPending;

    private bool counterWindow;           // 물기 선딜 (카운터 가능 구간)
    private bool counterTriggered;
    private bool lastHitWasHead;

    private bool frenzyDone;              // 최종 발악은 한 번만
    private readonly Dictionary<Player, float> corrodedUntil = new Dictionary<Player, float>();
    private readonly HashSet<DragonCover> corrodedThisBreath = new HashSet<DragonCover>();

    private float groundY;
    private Vector3 arenaCenter;
    private bool hasArenaCenter;

    private readonly Dictionary<Player, float> freezeGauges = new Dictionary<Player, float>();
    private readonly List<Player> freezeKeys = new List<Player>();
    private readonly HashSet<Player> struck = new HashSet<Player>();
    private readonly List<Player> playerBuffer = new List<Player>();

    private MobHitFlash flash;
    private AudioSource sfxSource;
    private AudioSource loopSource;

    private DragonStatData DragonData => statData as DragonStatData;

    private static readonly List<DragonBoss> s_active = new List<DragonBoss>();
    /// <summary>풀에서 나와 있는 드래곤 전체 (보스 체력바가 표시할 보스를 고른다).</summary>
    public static IReadOnlyList<DragonBoss> Active => s_active;

    #region Properties (상태 클래스·HUD에서 사용)
    /// <summary>스탯 SO. 주입 전이면 null.</summary>
    public DragonStatData Data => DragonData;

    /// <summary>무력화 게이지 비율(0~1). 공중에서는 격추 게이지 비율.</summary>
    public float PoiseRatio
    {
        get
        {
            var d = DragonData;
            if (d == null) return 1f;
            return isAirborne ? Mathf.Clamp01(shootdownGauge / Mathf.Max(d.ShootdownMax, 1f))
                              : Mathf.Clamp01(poise / Mathf.Max(d.PoiseMax, 1f));
        }
    }
    /// <summary>지금 게이지가 격추 게이지인지 (HUD 색 구분용).</summary>
    public bool IsShootdownGauge => isAirborne;
    public bool IsGroggy => isGroggy;
    public bool IsAirborne => isAirborne;
    public bool IsShootdownComplete => isAirborne && shootdownGauge <= 0f;
    public bool IsTakeOffPending => takeOffPending;
    /// <summary>최종 발악에 들어갈 체력인지 (한 번만).</summary>
    public bool IsFrenzyPending
        => !frenzyDone && DragonData != null && DragonData.FrenzyHpRatio > 0f && !isAirborne
           && status != null && !status.IsDead && HpRatio <= DragonData.FrenzyHpRatio;
    public float SkillGap => DragonData != null ? DragonData.SkillGap : 1.2f;
    public float GroundY => groundY;

    /// <summary>아레나 중심. 보스전 관리자가 정해 주고, 없으면 처음 자리 잡은 위치를 쓴다.</summary>
    public Vector3 ArenaCenter => hasArenaCenter ? arenaCenter : transform.position;

    /// <summary>타깃과의 수평 거리. 타깃이 없으면 무한대.</summary>
    public float TargetDistance => Target != null ? PlanarDistanceToTarget() : float.MaxValue;

    /// <summary>입 위치 (탄·브레스 시작점).</summary>
    public Vector3 MouthPosition
    {
        get
        {
            if (headBone == null) return transform.position + Vector3.up * 3f + transform.forward * 5f;
            return headBone.position + SnoutDirection * mouthForwardOffset;
        }
    }

    /// <summary>
    /// 주둥이가 향한 수평 방향. 목 → 머리 벡터의 수평 성분이라 브레스 클립의 좌우 흔들림이 그대로 반영된다.
    /// </summary>
    public Vector3 SnoutDirection
    {
        get
        {
            if (headBone == null || neckBone == null) return transform.forward;
            Vector3 d = headBone.position - neckBone.position;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
        }
    }
    #endregion

    protected override void Awake()
    {
        base.Awake();
        runBoolHash = ToAnimHash(runBool);
        spitBoolHash = ToAnimHash(spitBool);
        breathBoolHash = ToAnimHash(breathBool);
        hitHeavyBoolHash = ToAnimHash(hitHeavyBool);
        takeOffBoolHash = ToAnimHash(takeOffBool);
        flyIdleBoolHash = ToAnimHash(flyIdleBool);
        flySpitBoolHash = ToAnimHash(flySpitBool);
        flyBreathBoolHash = ToAnimHash(flyBreathBool);
        fallBoolHash = ToAnimHash(fallBool);
        landBoolHash = ToAnimHash(landBool);
        glideBoolHash = ToAnimHash(glideBool);
        glideLandBoolHash = ToAnimHash(glideLandBool);
        crashBoolHash = ToAnimHash(crashBool);

        biteStateHash = ToAnimHash(biteStateName);
        spitStateHash = ToAnimHash(spitStateName);
        breathStateHash = ToAnimHash(breathStateName);
        takeOffStateHash = ToAnimHash(takeOffStateName);
        flySpitStateHash = ToAnimHash(flySpitStateName);
        flyBreathStateHash = ToAnimHash(flyBreathStateName);
        landStateHash = ToAnimHash(landStateName);
        glideLandStateHash = ToAnimHash(glideLandStateName);
        crashStateHash = ToAnimHash(crashStateName);

        flash = GetComponent<MobHitFlash>();

        sfxSource = gameObject.AddComponent<AudioSource>();
        loopSource = gameObject.AddComponent<AudioSource>();
        foreach (var src in new[] { sfxSource, loopSource })
        {
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = 10f;
            src.maxDistance = 80f;
            src.rolloffMode = AudioRolloffMode.Linear;
        }
        loopSource.loop = true;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        for (int i = 0; i < cooldowns.Length; i++)
            cooldowns[i] = 0f;

        poise = DragonData != null ? DragonData.PoiseMax : 600f;
        poiseRegenWait = 0f;
        damageMultiplier = 1f;
        isGroggy = false;
        isInvulnerable = false;
        isAirborne = false;
        shootdownGauge = 0f;
        takeOffIndex = 0;
        takeOffPending = false;
        counterWindow = false;
        counterTriggered = false;
        hasArenaCenter = false;
        groundY = transform.position.y;
        freezeGauges.Clear();
        corrodedUntil.Clear();
        frenzyDone = false;
        breathFx?.SetEmitting(false);

        // 클라이언트도 복제된 드래곤이 스폰될 때 여기를 지나므로 모든 피어에 체력바가 뜬다
        if (!s_active.Contains(this)) s_active.Add(this);
        UI_Hud_BossHealth.EnsureShown();
    }

    public override void OnDespawn()
    {
        s_active.Remove(this);
        base.OnDespawn();
    }

    protected override void OnDestroy()
    {
        s_active.Remove(this);
        base.OnDestroy();
    }

    public override void ApplyStatData(EntityStatData data)
    {
        base.ApplyStatData(data);
        poise = DragonData != null ? DragonData.PoiseMax : 600f;
    }

    protected override void OnStatusInitialized()
    {
        base.OnStatusInitialized();
        if (status != null)
            status.OnDamaged += HandleGaugeDamage;
    }

    protected override MonsterStateMachine CreateStateMachine()
        => new DragonStateMachine(this);

    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.Run            => runBoolHash,
        MonsterAnimId.RangeAttack    => spitBoolHash,
        MonsterAnimId.Breath         => breathBoolHash,
        MonsterAnimId.Frenzy         => breathBoolHash, // 같은 클립(루프)을 계속 튼다
        MonsterAnimId.HitHeavy       => hitHeavyBoolHash,
        MonsterAnimId.TakeOff        => takeOffBoolHash,
        MonsterAnimId.FlyIdle        => flyIdleBoolHash,
        MonsterAnimId.FlyRangeAttack => flySpitBoolHash,
        MonsterAnimId.FlyBreath      => flyBreathBoolHash,
        MonsterAnimId.Fall           => fallBoolHash,
        MonsterAnimId.Land           => landBoolHash,
        MonsterAnimId.Glide          => glideBoolHash,
        MonsterAnimId.GlideLand      => glideLandBoolHash,
        MonsterAnimId.Crash          => crashBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    /// <summary>보스전 관리자가 아레나 중심을 지정한다. 지면 높이도 이 점을 기준으로 잡는다.</summary>
    public void SetArenaCenter(Vector3 center)
    {
        arenaCenter = center;
        hasArenaCenter = true;
        groundY = center.y;
    }

    protected override void OnGameUpdate(float deltaTime)
    {
        // 브레스 연출은 클라도 애니메이터 상태를 보고 각자 켠다
        UpdateBreathFx();

        if (IsSimulatedPeer)
        {
            if (!hasArenaCenter)
                SetArenaCenter(transform.position);

            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] > 0f)
                    cooldowns[i] -= deltaTime;
            }

            PoiseRegenStep(deltaTime);
            FreezeDecayStep(deltaTime);
            CheckTakeOff();
        }

        base.OnGameUpdate(deltaTime);
    }

    protected override void OnDeath()
    {
        breathFx?.SetEmitting(false);
        loopSource.Stop();
        DragonHazardZone.ClearAll();
        base.OnDeath();
    }

    #region 애니메이션 · 소리
    /// <summary>
    /// 애니메이터가 실제로 해당 클립(상태)에 들어갔는지. Bool → Exit → Entry 경로라 클립 시작까지 지연이 있어서
    /// 판정 시간은 이 값이 true가 된 순간부터 센다. 매핑이 없거나 애니메이터가 없으면 바로 true.
    /// </summary>
    public bool IsInClip(MonsterAnimId animId)
    {
        if (Animator == null) return true;
        int hash = animId switch
        {
            MonsterAnimId.Attack         => biteStateHash,
            MonsterAnimId.RangeAttack    => spitStateHash,
            MonsterAnimId.Breath         => breathStateHash,
            MonsterAnimId.TakeOff        => takeOffStateHash,
            MonsterAnimId.FlyRangeAttack => flySpitStateHash,
            MonsterAnimId.FlyBreath      => flyBreathStateHash,
            MonsterAnimId.Land           => landStateHash,
            MonsterAnimId.GlideLand      => glideLandStateHash,
            MonsterAnimId.Crash          => crashStateHash,
            _ => 0,
        };
        if (hash == 0) return true;
        return Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
    }

    // 모든 피어에서 불린다 (호스트는 PlayAnim, 클라는 복제된 애니 ID). 연출·소리는 여기서.
    protected override void OnAnimChanged(MonsterAnimId animId)
    {
        switch (animId)
        {
            case MonsterAnimId.Attack:
                PlaySfx(biteSfx);
                // 카운터 예고: 물기 선딜 동안 파랗게 빛난다
                flash?.FlashCounter(DragonData != null ? DragonData.BiteHitTime : 0.55f);
                break;
            case MonsterAnimId.RangeAttack:
            case MonsterAnimId.FlyRangeAttack:
                PlaySfx(spitSfx);
                break;
            case MonsterAnimId.TakeOff:
                PlaySfx(roarSfx);
                PlaySfx(wingSfx);
                break;
            case MonsterAnimId.Land:
            case MonsterAnimId.GlideLand:
                PlaySfx(landSfx);
                break;
            case MonsterAnimId.Crash:
                PlaySfx(crashSfx);
                break;
            case MonsterAnimId.HitHeavy:
                PlaySfx(counterSfx);
                break;
            case MonsterAnimId.Dead:
                PlaySfx(deathSfx);
                break;
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.volume = Extensions.GetSFXVolume();
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 브레스 클립의 "뿜는 구간"에만 파티클과 루프음을 켠다.
    /// 클라도 애니메이터 상태를 직접 보므로 따로 복제하지 않아도 된다.
    /// </summary>
    private void UpdateBreathFx()
    {
        var d = DragonData;
        bool emit = false;
        if (CurrentAnimId == MonsterAnimId.Frenzy && (status == null || !status.IsDead))
        {
            emit = true;
        }
        else if (d != null && Animator != null && (status == null || !status.IsDead))
        {
            var info = Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == breathStateHash)
                emit = InWindow(info, d.BreathStartTime, d.BreathEndTime);
            else if (info.shortNameHash == flyBreathStateHash)
                emit = InWindow(info, d.FlyBreathStartTime, d.FlyBreathEndTime);
        }

        breathFx?.SetEmitting(emit);

        if (emit && breathSfx != null && !loopSource.isPlaying)
        {
            loopSource.clip = breathSfx;
            loopSource.volume = Extensions.GetSFXVolume();
            loopSource.Play();
        }
        else if (!emit && loopSource.isPlaying)
        {
            loopSource.Stop();
        }
    }

    private static bool InWindow(AnimatorStateInfo info, float start, float end)
    {
        float t = info.normalizedTime * info.length; // 브레스 클립은 루프하지 않으므로 그대로 초 단위
        return t >= start && t <= end;
    }
    #endregion

    #region 데미지 · 무력화 · 카운터
    public override bool CanDamage(DamageContext context)
        => !isInvulnerable && base.CanDamage(context);

    public override void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

        lastHitWasHead = IsHeadHit(context.Point);

        // 카운터: 선딜 동안 머리(또는 정면 가까이)를 맞혔다
        if (counterWindow && (lastHitWasHead || IsFrontHit(context.Point)))
        {
            counterWindow = false;
            counterTriggered = true;
        }

        int amount = context.Amount;
        if (damageMultiplier != 1f)
            amount = Mathf.RoundToInt(amount * damageMultiplier);

        // 공중에서는 죽지 않는다 (추락·착지 후에 마무리한다)
        if (isAirborne && status != null)
        {
            float lethal = status.CurrentHp - 1f + status.Defense;
            if (amount > lethal) amount = Mathf.Max(Mathf.FloorToInt(lethal), 0);
            if (amount <= status.Defense) return;
        }

        if (amount != context.Amount)
            context = new DamageContext(context.Instigator, context.Point, amount, context.ToolId,
                context.ActionType, context.HarvestableNodeTypes);

        base.ApplyDamage(context);
    }

    private bool IsHeadHit(Vector3 point)
    {
        if (headBone == null || DragonData == null) return false;
        return (point - headBone.position).sqrMagnitude <= DragonData.HeadHitRadius * DragonData.HeadHitRadius;
    }

    // 멀티 클라 보고는 Point에 공격자 위치가 오므로, 정면 가까이에서 친 공격도 머리 공격으로 본다
    private bool IsFrontHit(Vector3 point)
    {
        var d = DragonData;
        if (d == null) return false;
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > d.CounterFrontReach * d.CounterFrontReach) return false;
        return Vector3.Angle(transform.forward, to) <= d.CounterFrontAngle * 0.5f;
    }

    // 받은 피해로 무력화(지상) 또는 격추(공중) 게이지를 깎는다.
    private void HandleGaugeDamage(float amount)
    {
        if (!IsSimulatedPeer || DragonData == null) return;
        if (status == null || status.IsDead || isInvulnerable) return;

        if (isAirborne)
        {
            shootdownGauge = Mathf.Max(shootdownGauge - amount, 0f);
            return;
        }

        // 최종 발악은 죽을 때까지 끊기지 않는다
        if (isGroggy || stateMachine?.CurrentState is DragonFrenzyState) return;
        DamagePoise(amount * (lastHitWasHead ? DragonData.HeadPoiseMultiplier : 1f));
    }

    private void DamagePoise(float amount)
    {
        var d = DragonData;
        poise -= amount;
        poiseRegenWait = d.PoiseRegenDelay;
        if (poise > 0f) return;

        poise = 0f;
        (stateMachine as DragonStateMachine)?.ToGroggy(d.GroggyDuration, d.GroggyDamageMultiplier);
    }

    private void PoiseRegenStep(float deltaTime)
    {
        var d = DragonData;
        if (d == null || isGroggy || isAirborne) return;

        if (poiseRegenWait > 0f)
        {
            poiseRegenWait -= deltaTime;
            return;
        }
        poise = Mathf.Min(poise + d.PoiseRegenPerSecond * deltaTime, d.PoiseMax);
    }

    /// <summary>그로기를 켜고 끈다. 끝날 때 무력화 게이지를 가득 채운다. (DragonGroggyState가 호출)</summary>
    public void SetGroggy(bool value, float multiplier = 1f)
    {
        isGroggy = value;
        damageMultiplier = value ? multiplier : 1f;

        if (!value && DragonData != null)
        {
            poise = DragonData.PoiseMax;
            poiseRegenWait = 0f;
        }
    }

    public void SetInvulnerable(bool value) => isInvulnerable = value;

    /// <summary>카운터 구간을 열고 닫는다. 열 때 이전 판정을 지운다.</summary>
    public void SetCounterWindow(bool open)
    {
        counterWindow = open;
        if (open) counterTriggered = false;
    }

    /// <summary>카운터가 들어왔는지 확인하고 소비한다. 들어왔으면 무력화를 크게 깎는다.</summary>
    public bool ConsumeCounter()
    {
        if (!counterTriggered) return false;
        counterTriggered = false;
        counterWindow = false;
        if (DragonData != null && !isGroggy)
        {
            poise -= DragonData.CounterPoiseDamage;
            poiseRegenWait = DragonData.PoiseRegenDelay;
            if (poise < 0f) poise = 0f;
        }
        return true;
    }

    /// <summary>카운터 경직이 끝났을 때 무력화가 바닥났으면 그로기로 넘길지.</summary>
    public bool IsPoiseBroken => poise <= 0f;
    #endregion

    #region 공중 페이즈
    private void CheckTakeOff()
    {
        var d = DragonData;
        if (d == null || d.TakeOffHpRatios == null || takeOffPending || isAirborne) return;
        if (status == null || status.IsDead) return;
        if (takeOffIndex >= d.TakeOffHpRatios.Length) return;
        if (HpRatio > d.TakeOffHpRatios[takeOffIndex]) return;

        takeOffIndex++;
        takeOffPending = true;
    }

    /// <summary>이륙을 시작한다 (DragonTakeOffState가 호출). 격추 게이지를 채운다.</summary>
    public void BeginAirPhase()
    {
        takeOffPending = false;
        isAirborne = true;
        shootdownGauge = DragonData != null ? DragonData.ShootdownMax : 300f;
        breathFx?.SetEmitting(false);
    }

    /// <summary>땅에 내려왔다 (추락·착지). 무력화 게이지를 가득 채운다.</summary>
    public void EndAirPhase()
    {
        isAirborne = false;
        SetAltitude(0f);
        if (DragonData != null) poise = DragonData.PoiseMax;
    }

    /// <summary>지면 기준 고도(m)로 루트를 옮긴다.</summary>
    public void SetAltitude(float height)
    {
        Vector3 p = transform.position;
        p.y = groundY + height;
        transform.position = p;
    }

    public float Altitude => transform.position.y - groundY;

    /// <summary>수평 이동 (고도 유지).</summary>
    public void MovePlanar(Vector3 delta)
    {
        delta.y = 0f;
        transform.position += delta;
    }

    /// <summary>수평 방향을 바라보도록 돈다 (회전 속도 제한).</summary>
    public void TurnTowards(Vector3 flatDir, float deltaTime, float degPerSec)
    {
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(flatDir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degPerSec * deltaTime);
    }

    /// <summary>지금 몸통 yaw에 각도를 더한다 (브레스 쓸기).</summary>
    public void AddYaw(float degrees) => transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + degrees, 0f);
    #endregion

    #region 타깃 · 쿨타임
    public bool IsSkillReady(DragonSkill skill) => cooldowns[(int)skill] <= 0f;

    public void StartCooldown(DragonSkill skill)
    {
        var d = DragonData;
        if (d == null) return;
        cooldowns[(int)skill] = skill switch
        {
            DragonSkill.Bite   => d.BiteCooldown,
            DragonSkill.Spit   => d.SpitCooldown,
            DragonSkill.Breath => d.BreathCooldown,
            DragonSkill.Charge => d.ChargeCooldown,
            _ => 0f,
        };
    }

    /// <summary>아레나 안의 살아 있는 플레이어를 가까운 순으로 모은다. 반환 리스트는 재사용되므로 바로 쓸 것.</summary>
    public List<Player> CollectPlayers()
    {
        playerBuffer.Clear();
        struck.Clear();
        float range = Mathf.Max(DetectRange, 10f);
        foreach (var col in Physics.OverlapSphere(ArenaCenter, range, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive || !struck.Add(player)) continue;
            playerBuffer.Add(player);
        }

        Vector3 me = transform.position;
        playerBuffer.Sort((a, b) => PlanarSqr(a.transform.position - me).CompareTo(PlanarSqr(b.transform.position - me)));
        return playerBuffer;
    }

    private static float PlanarSqr(Vector3 v) => v.x * v.x + v.z * v.z;
    #endregion

    #region 판정 (호스트/싱글 전용)
    /// <summary>서리 물기: 정면 부채꼴 안의 플레이어 전원.</summary>
    public void BiteHit()
    {
        var d = DragonData;
        if (d == null) return;

        // 판정 중심은 입 아래 땅이 아니라 몸 앞쪽 — 사거리는 루트에서 잰다
        foreach (var player in PlayersInFan(transform.position, transform.forward, d.BiteRange, d.BiteAngle))
        {
            DealDamage(player, d.BiteDamage);
            if (d.BiteCorrosionMultiplier > 1f)
                corrodedUntil[player] = Time.time + d.BiteCorrosionDuration;
            Vector3 push = player.transform.position - transform.position;
            player.Motor?.AddKnockback(push, d.BiteKnockback, 0.3f);
        }
    }

    /// <summary>
    /// 브레스 판정 1틱. 몸통(루트) 기준 주둥이 방향 부채꼴(수평) 안에 있고 엄폐물에 가려지지 않은 플레이어에게 피해 + 빙결 게이지.
    /// 원점을 입이 아니라 루트로 잡는 이유: 쓸기는 몸통 회전이라, 입 앞 가까운 플레이어는 입 기준 각도가 너무 빨리 지나가 거의 안 맞는다.
    /// 사거리(range)는 입에서부터 잰 거리라 루트~입 거리만큼 늘려 판정한다.
    /// </summary>
    public void BreathTick(float range)
    {
        var d = DragonData;
        if (d == null) return;

        Vector3 mouth = MouthPosition;
        Vector3 dir = SnoutDirection;
        Vector3 toMouth = mouth - transform.position;
        toMouth.y = 0f;

        if (d.BreathCorrodesCover)
            CorrodeCoversInFan(dir, range + toMouth.magnitude, d.BreathHalfAngle);

        foreach (var player in PlayersInFan(transform.position, dir, range + toMouth.magnitude, d.BreathHalfAngle * 2f))
        {
            if (DragonCover.IsBlocked(mouth, player.transform.position + Vector3.up * 1f)) continue;

            DealDamage(player, d.BreathTickDamage);

            freezeGauges.TryGetValue(player, out float gauge);
            gauge += d.FreezePerTick;
            if (gauge >= 1f)
            {
                gauge = 0f;
                player.ApplySlow(0f, d.FreezeDuration); // 빙결: 이동 불가
            }
            freezeGauges[player] = gauge;
        }
    }

    /// <summary>브레스 1회를 시작한다. 이번 브레스에서 부식시킨 기둥 목록을 비운다.</summary>
    public void BeginBreath() => corrodedThisBreath.Clear();

    // 브레스 부채꼴 안의 살아 있는 기둥을 브레스 1회당 한 번씩 부식시킨다
    private void CorrodeCoversInFan(Vector3 dir, float range, float halfAngle)
    {
        foreach (var cover in DragonCover.All)
        {
            if (cover == null || !cover.IsIntact || corrodedThisBreath.Contains(cover)) continue;
            Vector3 to = cover.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > range * range) continue;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(dir, to) > halfAngle + 5f) continue;

            corrodedThisBreath.Add(cover);
            cover.Corrode();
        }
    }

    /// <summary>
    /// 이 보스가 플레이어에게 주는 피해에 부식 배율을 건다. 탄·장판도 이걸 거쳐 피해를 준다.
    /// </summary>
    public int ModifyOutgoingDamage(Player player, int damage)
    {
        var d = DragonData;
        if (d == null || player == null || d.BiteCorrosionMultiplier <= 1f) return damage;
        if (!corrodedUntil.TryGetValue(player, out float until)) return damage;
        if (Time.time > until)
        {
            corrodedUntil.Remove(player);
            return damage;
        }
        return Mathf.RoundToInt(damage * d.BiteCorrosionMultiplier);
    }

    private void FreezeDecayStep(float deltaTime)
    {
        if (freezeGauges.Count == 0 || DragonData == null) return;
        freezeKeys.Clear();
        freezeKeys.AddRange(freezeGauges.Keys);
        foreach (var p in freezeKeys)
        {
            float g = freezeGauges[p] - DragonData.FreezeDecayPerSecond * deltaTime;
            if (g <= 0f || p == null) freezeGauges.Remove(p);
            else freezeGauges[p] = g;
        }
    }

    /// <summary>
    /// 얼음탄 1발: 입에서 target(지면)으로 날린다. 착탄 지점에 예고 원을 깔고, 착탄하면 얼음 장판을 남긴다.
    /// </summary>
    public void FireFrostBall(Vector3 target, float flightTime, bool bombard = false)
    {
        var d = DragonData;
        if (d == null) return;
        target.y = groundY;

        SpawnMarkerCircle(target, d.SpitImpactRadius, flightTime, DragonMarkerStyle.Danger);
        FireFrostBallAsync(MouthPosition, target, flightTime, bombard ? d.BombardPatchDuration : d.IcePatchDuration).Forget();
    }

    private async UniTaskVoid FireFrostBallAsync(Vector3 origin, Vector3 target, float flightTime, float patchDuration)
    {
        var d = DragonData;
        var ball = await Extensions.SpawnAsync<DragonFrostBall>(d.FrostBallKey);
        if (ball == null) return;

        var zone = new DragonFrostBall.ZoneSettings
        {
            Key = d.IcePatchKey,
            Radius = d.IcePatchRadius,
            Duration = patchDuration,
            SlowMultiplier = d.IcePatchSlow,
            DotPerSecond = d.PatchDotPerSecond,
        };
        ball.Init(gameObject, origin, target, flightTime, d.SpitImpactRadius,
            Mathf.Max(Mathf.RoundToInt(d.SpitDamage), 0), zone);
    }

    /// <summary>
    /// 돌진 1프레임 판정: 몸 주변(폭 ChargeWidth) 플레이어를 한 번씩 치고 옆으로 날린다.
    /// hitOnce는 이번 돌진에서 이미 맞은 플레이어 목록(상태가 들고 있는다).
    /// </summary>
    public void ChargeHit(HashSet<Player> hitOnce)
    {
        var d = DragonData;
        if (d == null) return;

        float half = d.ChargeWidth * 0.5f;
        Vector3 center = transform.position + transform.forward * 2f;
        foreach (var col in Physics.OverlapSphere(center, half + 4f, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive || hitOnce.Contains(player)) continue;

            // 몸 길이 방향(앞뒤 ±4m) · 폭 방향(±half) 직사각형 안인지
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            if (Mathf.Abs(local.x) > half || local.z < -4f || local.z > 6f) continue;

            hitOnce.Add(player);
            DealDamage(player, d.ChargeDamage);
            Vector3 side = transform.right * Mathf.Sign(local.x == 0f ? 1f : local.x);
            player.Motor?.AddKnockback(side + transform.forward * 0.5f, d.ChargeKnockback, 0.35f);
        }
    }

    /// <summary>돌진 경로에 장판을 남긴다 (2페이즈 망령 돌진).</summary>
    public void DropTrailPatch(Vector3 position)
    {
        var d = DragonData;
        if (d == null || string.IsNullOrEmpty(d.IcePatchKey)) return;
        position.y = groundY;
        SpawnPatchAsync(position, d.IcePatchRadius, d.IcePatchDuration, d.IcePatchSlow, d.PatchDotPerSecond).Forget();
    }

    private async UniTaskVoid SpawnPatchAsync(Vector3 center, float radius, float duration, float slow, float dot)
    {
        var zone = await Extensions.SpawnAsync<DragonHazardZone>(DragonData.IcePatchKey);
        if (zone == null) return;
        zone.Init(gameObject, center, radius, duration, slow, dot);
    }

    /// <summary>월식 폭우 시작: 아레나 전체를 덮는 연출 장판 + 기둥 안전지대 표시.</summary>
    public void BeginAcidRain()
    {
        var d = DragonData;
        if (d == null) return;
        PlaySfx(explosionSfx);
        if (!string.IsNullOrEmpty(d.IcePatchKey))
            SpawnPatchAsync(transform.position, d.ExplosionRadius, d.AcidRainDuration, 1f, 0f).Forget(); // 판정 없는 연출
        foreach (var cover in DragonCover.All)
        {
            if (cover == null || !cover.IsIntact) continue;
            Vector3 c = cover.transform.position;
            c.y = groundY;
            SpawnMarkerCircle(c, cover.SafeRadius, d.AcidRainDuration, DragonMarkerStyle.Safe);
        }
    }

    /// <summary>월식 폭우 1틱: 살아 있는 기둥 안전지대 밖의 전원에게 피해.</summary>
    public void AcidRainTick()
    {
        var d = DragonData;
        if (d == null) return;
        foreach (var player in CollectPlayers())
        {
            if (DragonCover.IsInSafeZone(player.transform.position)) continue;
            DealDamage(player, d.AcidRainTickDamage);
        }
    }

    /// <summary>광폭화: 전원에게 최대 체력 비율 피해 (최종 발악 시간 초과).</summary>
    public void EnrageHit()
    {
        var d = DragonData;
        if (d == null) return;
        PlaySfx(explosionSfx);
        SpawnVfx(explosionVfxKey, transform.position, 6f);
        foreach (var player in CollectPlayers())
        {
            float maxHp = player.Stat != null ? player.Stat.MaxHp : 150f;
            DealDamage(player, maxHp * d.EnrageHpRatio);
        }
    }

    /// <summary>등장 착지 충격파: 주변을 밀어낸다(피해 없음).</summary>
    public void IntroShockwave()
    {
        var d = DragonData;
        if (d == null) return;
        foreach (var player in CollectPlayers())
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > d.IntroShockwaveRadius * d.IntroShockwaveRadius) continue;
            player.Motor?.AddKnockback(to, d.IntroShockwaveKnockback, 0.4f);
        }
        SpawnVfx(crashVfxKey, transform.position + transform.forward * 2f, 3f);
    }

    /// <summary>최종 발악을 시작한다 (DragonFrenzyState가 호출).</summary>
    public void ConsumeFrenzy() => frenzyDone = true;

    /// <summary>페이즈 전환으로 등장할 때 보스전 관리자가 부른다: 이륙 → 포효 → 착지 충격파 → 교전.</summary>
    public void BeginIntro() => (stateMachine as DragonStateMachine)?.ToIntro();

    /// <summary>돌진 중 몸 앞에 엄폐물(기둥)이 있으면 돌려준다.</summary>
    public DragonCover FindCoverAhead()
        => DragonCover.FindOverlap(transform.position + transform.forward * 4.5f + Vector3.up * 1.5f, 1.8f);

    /// <summary>추락 충돌: 주변 플레이어를 밀어낸다(피해 없음) + 충돌 이펙트.</summary>
    public void CrashImpact()
    {
        var d = DragonData;
        if (d == null) return;

        foreach (var player in CollectPlayers())
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > d.CrashKnockbackRadius * d.CrashKnockbackRadius) continue;
            player.Motor?.AddKnockback(to, d.CrashKnockback, 0.35f);
        }

        SpawnVfx(crashVfxKey, transform.position + transform.forward * 2f, 3f);
    }

    /// <summary>서리 폭발 예고: 아레나 전체 경고 원 + 기둥 안전지대 원.</summary>
    public void BeginExplosionWarning()
    {
        var d = DragonData;
        if (d == null) return;
        SpawnMarkerCircle(transform.position, d.ExplosionRadius, d.ExplosionWarningTime, DragonMarkerStyle.Wipe);
        foreach (var cover in DragonCover.All)
        {
            if (cover == null || !cover.IsIntact) continue;
            Vector3 c = cover.transform.position;
            c.y = groundY;
            SpawnMarkerCircle(c, cover.SafeRadius, d.ExplosionWarningTime, DragonMarkerStyle.Safe);
        }
    }

    /// <summary>서리 폭발 전멸기: 반경 안 전원에게 최대 체력 비율 피해. 기둥 안전지대 안이면 줄어든다.</summary>
    public void ExplosionHit()
    {
        var d = DragonData;
        if (d == null) return;

        PlaySfx(explosionSfx);
        SpawnVfx(explosionVfxKey, transform.position, 6f);

        foreach (var player in CollectPlayers())
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > d.ExplosionRadius * d.ExplosionRadius) continue;

            float ratio = DragonCover.IsInSafeZone(player.transform.position) ? d.ExplosionSafeHpRatio : d.ExplosionHpRatio;
            float maxHp = player.Stat != null ? player.Stat.MaxHp : 150f;
            DealDamage(player, maxHp * ratio);
        }
    }

    private List<Player> PlayersInFan(Vector3 origin, Vector3 forward, float range, float angle)
    {
        var result = new List<Player>();
        struck.Clear();
        forward.y = 0f;
        float half = angle * 0.5f;

        foreach (var col in Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive || !struck.Add(player)) continue;

            Vector3 to = player.transform.position - origin;
            to.y = 0f;
            if (to.sqrMagnitude > range * range) continue;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(forward, to) > half) continue;
            result.Add(player);
        }
        return result;
    }

#if UNITY_EDITOR
    /// <summary>에디터 테스트용: 드래곤이 플레이어에게 준 피해를 콘솔에 남긴다 (DragonBossDebugMenu에서 켠다).</summary>
    public static bool DebugLogHits;
#endif

    private void DealDamage(Player player, float amount)
    {
        int dmg = ModifyOutgoingDamage(player, Mathf.Max(Mathf.RoundToInt(amount), 0));
#if UNITY_EDITOR
        if (DebugLogHits)
            Debug.Log($"[DragonHit] {stateMachine?.CurrentState?.GetType().Name} anim={CurrentAnimId} dmg={dmg} t={Time.time:0.00}");
#endif
        var ctx = new DamageContext(gameObject, player.transform.position, dmg, string.Empty);
        if (player.TryGetComponent<IDamageable>(out var target) && target.CanDamage(ctx))
            target.ApplyDamage(ctx);
    }
    #endregion

    #region 이펙트 생성
    public void SpawnMarkerCircle(Vector3 center, float radius, float duration, DragonMarkerStyle style)
        => SpawnMarkerAsync(center, center, radius, duration, style, circle: true).Forget();

    public void SpawnMarkerLine(Vector3 start, Vector3 end, float width, float duration)
        => SpawnMarkerAsync(start, end, width, duration, DragonMarkerStyle.Danger, circle: false).Forget();

    private async UniTaskVoid SpawnMarkerAsync(Vector3 a, Vector3 b, float size, float duration, DragonMarkerStyle style, bool circle)
    {
        if (string.IsNullOrEmpty(markerKey)) return;
        var marker = await Extensions.SpawnAsync<DragonGroundMarker>(markerKey);
        if (marker == null) return;
        if (circle) marker.InitCircle(a, size, duration, style);
        else marker.InitLine(a, b, size, duration, style);
    }

    private void SpawnVfx(string key, Vector3 position, float scale)
    {
        if (string.IsNullOrEmpty(key)) return;
        SpawnVfxAsync(key, position, scale).Forget();
    }

    private static async UniTaskVoid SpawnVfxAsync(string key, Vector3 position, float scale)
    {
        var vfx = await Extensions.SpawnAsync<DragonVfx>(key);
        if (vfx == null) return;
        vfx.Play(position, scale);
    }
    #endregion
}

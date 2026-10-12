using UnityEngine;

/// <summary>
/// 외눈 박쥐 메이지의 회복 스킬 연출: 발밑에서 초록 원이 반경까지 퍼지며 사라진다. 판정은 없다(회복은 BatMage가 처리).
/// 원 2개(바깥으로 퍼지는 파동 + 반경 표시 원)를 LineRenderer로 그린다.
/// 멀티: 호스트가 띄우면 NetworkMonsterDirector가 클라에도 같은 연출을 띄운다. (MageStarLaser와 같은 방식)
/// </summary>
public class MageHealCircle : MonoBehaviour, IPoolable
{
    #region Inspector
    [Tooltip("선 머티리얼 (MageStarLaser와 같은 것 — Sprites/Default, 렌더 큐 3100)")]
    [SerializeField] private Material lineMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color color = new Color(0.35f, 1.6f, 0.6f, 0.9f);
    [Tooltip("선의 보이는 두께(m)")]
    [SerializeField] private float width = 0.12f;
    [Tooltip("파동이 반경까지 퍼지는 시간(초)")]
    [SerializeField] private float expandTime = 0.6f;
    [Tooltip("다 퍼진 뒤 사라지는 시간(초)")]
    [SerializeField] private float fadeTime = 0.6f;
    [Tooltip("바닥에서 띄우는 높이(m)")]
    [SerializeField] private float groundOffset = 0.08f;

    [Header("효과음 (3D)")]
    [SerializeField] private AudioClip healSfx;
    #endregion

    private const int Segments = 64;

    private LineRenderer _wave;
    private LineRenderer _rim;
    private AudioSource _audio;
    private Vector3 center;
    private float radius;
    private float timer;
    private bool playing;
    private bool loopHooked;

    private void Awake()
    {
        _wave = CreateRing("Wave");
        _rim = CreateRing("Rim");

        _audio = gameObject.GetOrAddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
        _audio.minDistance = 6f;
        _audio.maxDistance = 40f;
        _audio.rolloffMode = AudioRolloffMode.Linear;

        SetVisible(false);
    }

    private LineRenderer CreateRing(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.material = lineMaterial;
        lr.alignment = LineAlignment.View; // 1인칭 눈높이에서도 보이게 (MageStarLaser 참고)
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    /// <summary>회복 연출을 시작한다. 호스트/싱글 원본이면 클라에도 같은 연출을 보낸다.</summary>
    public void Play(Vector3 center, float radius)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastHealCircle(Main.Pool.GetAddress(gameObject), center, radius);
#endif
        PlayVisual(center, radius);
    }

    /// <summary>멀티 클라 전용: 연출만 재생한다.</summary>
    public void PlayVisual(Vector3 center, float radius)
    {
        this.center = center;
        this.radius = Mathf.Max(radius, 0.5f);
        transform.position = center;
        timer = 0f;
        playing = true;

        SetRing(_rim, this.radius, 0f);
        SetRing(_wave, 0.1f, 1f);
        SetVisible(true);

        if (healSfx != null && _audio != null)
        {
            _audio.volume = Extensions.GetSFXVolume();
            _audio.PlayOneShot(healSfx);
        }
    }

    #region IPoolable
    public void OnSpawn() => playing = false;
    public void OnDespawn()
    {
        playing = false;
        SetVisible(false);
        if (_audio != null) _audio.Stop();
    }
    #endregion

    private void OnEnable()
    {
        if (loopHooked) return;
        Main.Loop.OnGameUpdate += HandleGameUpdate;
        loopHooked = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= HandleGameUpdate;
        loopHooked = false;
    }

    private void HandleGameUpdate(float deltaTime)
    {
        if (!playing || !isActiveAndEnabled) return;

        timer += deltaTime;
        float expand = Mathf.Clamp01(timer / Mathf.Max(expandTime, 0.01f));
        float fade = timer <= expandTime ? 1f : 1f - Mathf.Clamp01((timer - expandTime) / Mathf.Max(fadeTime, 0.01f));

        // 파동: 빠르게 퍼지다 감속(OutCubic)
        float eased = 1f - Mathf.Pow(1f - expand, 3f);
        SetRing(_wave, Mathf.Lerp(0.1f, radius, eased), fade);
        // 반경 표시 원: 퍼지는 동안 서서히 나타났다가 함께 사라진다
        SetRing(_rim, radius, Mathf.Min(expand, fade) * 0.6f);

        if (timer >= expandTime + fadeTime)
        {
            playing = false;
            SetVisible(false);
            Extensions.Despawn(gameObject);
        }
    }

    private void SetRing(LineRenderer lr, float r, float alpha)
    {
        float y = center.y + groundOffset;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * r, y, center.z + Mathf.Cos(a) * r));
        }
        Color c = color;
        c.a *= alpha;
        lr.startColor = c;
        lr.endColor = c;
        lr.startWidth = width;
        lr.endWidth = width;
    }

    private void SetVisible(bool visible)
    {
        if (_wave != null) _wave.enabled = visible;
        if (_rim != null) _rim.enabled = visible;
    }
}

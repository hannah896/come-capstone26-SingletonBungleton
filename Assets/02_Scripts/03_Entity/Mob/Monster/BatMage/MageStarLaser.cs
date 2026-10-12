using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외눈 박쥐 메이지의 스펠: 바닥에 별(펜타그램) 모양 마법 레이저를 깐다.
/// 예고(얇은 선 + 마법진 원) → 레이저(굵고 밝은 선, 켜지는 순간 1회 판정) → 회수.
///
/// 선은 LineRenderer로 런타임에 그린다(선 5개 + 바깥 원 1개). 판정은 플레이어의 수평 위치와
/// 별의 각 변(선분) 사이 거리로 하고, 높이는 무시한다.
///
/// 멀티: 호스트 원본만 판정하고, 클라에는 NetworkMonsterDirector가 연출용 복제본(InitVisual)을 띄운다. (Meteor와 같은 방식)
/// </summary>
public class MageStarLaser : MonoBehaviour, IPoolable
{
    private enum Phase { Idle, Warning, Laser }

    #region Inspector
    [Header("연출")]
    [Tooltip("선에 쓸 머티리얼 (알파 블렌딩 Unlit). 가산 블렌딩은 밝은 바닥 위에서 하얗게 날아가 안 보인다")]
    [SerializeField] private Material lineMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color warningColor = new Color(1f, 0.15f, 0.85f, 0.9f);
    [ColorUsage(true, true)] [SerializeField] private Color laserColor = new Color(1.6f, 0.45f, 2.2f, 1f);
    [Tooltip("예고선의 보이는 두께(m)")]
    [SerializeField] private float warningWidth = 0.07f;
    [Tooltip("레이저의 보이는 두께(m). 판정 폭(StarLaserWidth)과 별개 — 크게 하면 근처 선이 시야를 가린다")]
    [SerializeField] private float laserVisualWidth = 0.4f;
    [Tooltip("바닥에서 띄우는 높이(m). 지면에 묻히지 않게")]
    [SerializeField] private float groundOffset = 0.08f;

    [Header("효과음 (3D)")]
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField] private AudioClip fireSfx;
    #endregion

    private const int StarPoints = 5;
    private const int CircleSegments = 64;

    private readonly LineRenderer[] _edges = new LineRenderer[StarPoints];
    private LineRenderer _circle;
    private AudioSource _audio;
    private readonly Vector3[] _points = new Vector3[StarPoints];
    private readonly HashSet<Player> _struck = new HashSet<Player>();

    private GameObject owner;
    private Vector3 center;
    private float radius;
    private float laserWidth;
    private int damage;
    private float warningTime;
    private float laserTime;
    private float timer;
    private Phase phase = Phase.Idle;
    private bool visualOnly;
    private bool loopHooked;

    private void Awake()
    {
        for (int i = 0; i < StarPoints; i++)
            _edges[i] = CreateLine($"Edge{i}", 2);
        _circle = CreateLine("Circle", CircleSegments);
        _circle.loop = true;

        _audio = gameObject.GetOrAddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
        _audio.minDistance = 6f;
        _audio.maxDistance = 45f;
        _audio.rolloffMode = AudioRolloffMode.Linear;

        SetLinesVisible(false);
    }

    private LineRenderer CreateLine(string name, int count)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = count;
        lr.material = lineMaterial;
        lr.numCapVertices = 4;
        // 카메라를 향하게 그린다. 바닥에 납작하게 눕히면 1인칭 눈높이에서 비스듬히 보여 거의 안 보인다.
        // 대신 플레이어 바로 옆을 지나는 선이 두꺼워 보이지 않도록 보이는 두께는 판정 폭보다 가늘게 둔다(laserVisualWidth).
        // ※ 물 같은 투명 바닥에 가려지지 않도록 머티리얼 렌더 큐는 3100(투명 3000보다 뒤)으로 둔다.
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    #region Init
    /// <summary>
    /// 별 레이저를 깐다. yawDeg는 첫 꼭짓점 방향(도). 호스트/싱글 원본만 판정한다.
    /// </summary>
    public void Init(GameObject owner, Vector3 center, float yawDeg, float radius, float width, int damage,
                     float warningTime, float laserTime)
    {
#if PHOTON_FUSION
        // 멀티 호스트: 클라 화면에도 같은 별을 띄운다 (판정은 이 원본만 한다).
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastStarLaser(
                Main.Pool.GetAddress(gameObject), center, yawDeg, radius, width, warningTime, laserTime);
#endif
        Begin(owner, center, yawDeg, radius, width, damage, warningTime, laserTime, visual: false);
    }

    /// <summary>멀티 클라 전용: 호스트가 깐 별의 연출용 복제본. 데미지를 주지 않는다.</summary>
    public void InitVisual(Vector3 center, float yawDeg, float radius, float width, float warningTime, float laserTime)
        => Begin(null, center, yawDeg, radius, width, 0, warningTime, laserTime, visual: true);

    private void Begin(GameObject owner, Vector3 center, float yawDeg, float radius, float width, int damage,
                       float warningTime, float laserTime, bool visual)
    {
        this.owner = owner;
        this.center = center;
        this.radius = Mathf.Max(radius, 0.5f);
        laserWidth = Mathf.Max(width, 0.1f);
        this.damage = damage;
        this.warningTime = Mathf.Max(warningTime, 0f);
        this.laserTime = Mathf.Max(laserTime, 0.05f);
        visualOnly = visual;

        transform.position = center;
        BuildShape(yawDeg);

        phase = Phase.Warning;
        timer = this.warningTime;
        ApplyLook(warningColor, warningWidth);
        SetLinesVisible(true);
        PlaySfx(chargeSfx);

        if (timer <= 0f) Fire();
    }
    #endregion

    #region IPoolable
    public void OnSpawn() => phase = Phase.Idle; // Init 전까지 대기
    public void OnDespawn()
    {
        phase = Phase.Idle;
        SetLinesVisible(false);
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
        if (phase == Phase.Idle || !isActiveAndEnabled) return;

        timer -= deltaTime;

        if (phase == Phase.Warning)
        {
            // 터지기 직전일수록 빠르게 깜빡여 타이밍을 알려준다
            float t = warningTime > 0f ? 1f - timer / warningTime : 1f;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, t));
            Color c = warningColor;
            c.a *= pulse;
            ApplyLook(c, warningWidth);

            if (timer <= 0f) Fire();
            return;
        }

        // 레이저: 굵게 켜졌다가 가늘어지며 사라진다
        float k = Mathf.Clamp01(timer / laserTime);
        Color lc = laserColor;
        lc.a *= k;
        ApplyLook(lc, laserVisualWidth * Mathf.Lerp(0.3f, 1f, k));

        if (timer <= 0f) Finish();
    }

    private void Fire()
    {
        phase = Phase.Laser;
        timer = laserTime;
        ApplyLook(laserColor, laserVisualWidth);
        PlaySfx(fireSfx);

        if (!visualOnly)
            ApplyDamage();
    }

    private void Finish()
    {
        if (phase == Phase.Idle) return;
        phase = Phase.Idle;
        SetLinesVisible(false);
        Extensions.Despawn(gameObject);
    }

    #region Shape
    // 바깥 꼭짓점 5개를 구하고, i → i+2 로 이어 펜타그램을 만든다.
    private void BuildShape(float yawDeg)
    {
        float y = center.y + groundOffset;
        for (int i = 0; i < StarPoints; i++)
        {
            float a = (yawDeg + i * 360f / StarPoints) * Mathf.Deg2Rad;
            _points[i] = new Vector3(center.x + Mathf.Sin(a) * radius, y, center.z + Mathf.Cos(a) * radius);
        }

        for (int i = 0; i < StarPoints; i++)
        {
            _edges[i].SetPosition(0, _points[i]);
            _edges[i].SetPosition(1, _points[(i + 2) % StarPoints]);
        }

        for (int i = 0; i < CircleSegments; i++)
        {
            float a = i * Mathf.PI * 2f / CircleSegments;
            _circle.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * radius, y, center.z + Mathf.Cos(a) * radius));
        }
    }

    private void ApplyLook(Color color, float width)
    {
        foreach (var lr in _edges) SetLook(lr, color, width);
        // 바깥 원은 마법진 장식이라 판정이 없다 → 항상 얇고 흐리게
        Color cc = color;
        cc.a *= 0.5f;
        SetLook(_circle, cc, warningWidth);
    }

    private static void SetLook(LineRenderer lr, Color color, float width)
    {
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
    }

    private void SetLinesVisible(bool visible)
    {
        foreach (var lr in _edges) if (lr != null) lr.enabled = visible;
        if (_circle != null) _circle.enabled = visible;
    }
    #endregion

    #region Damage
    // 별의 어느 변이든 판정 폭 안에 서 있는 플레이어에게 1회 데미지
    private void ApplyDamage()
    {
        _struck.Clear();
        float half = laserWidth * 0.5f + 0.3f; // 0.3 = 플레이어 몸통 반경 정도
        var hits = Physics.OverlapSphere(center, radius + half, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive) continue;
            if (!_struck.Add(player)) continue;

            Vector3 p = player.transform.position;
            if (!IsOnStar(p, half)) continue;

            var ctx = new DamageContext(
                instigator: owner,
                point: p,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);
        }
    }

    private bool IsOnStar(Vector3 worldPos, float halfWidth)
    {
        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        float sqr = halfWidth * halfWidth;
        for (int i = 0; i < StarPoints; i++)
        {
            Vector3 a3 = _points[i];
            Vector3 b3 = _points[(i + 2) % StarPoints];
            if (SqrDistanceToSegment(p, new Vector2(a3.x, a3.z), new Vector2(b3.x, b3.z)) <= sqr)
                return true;
        }
        return false;
    }

    private static float SqrDistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
        return (p - (a + ab * t)).sqrMagnitude;
    }
    #endregion

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || _audio == null) return;
        _audio.volume = Extensions.GetSFXVolume();
        _audio.PlayOneShot(clip);
    }
}

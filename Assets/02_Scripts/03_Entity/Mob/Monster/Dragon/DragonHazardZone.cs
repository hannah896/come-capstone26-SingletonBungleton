using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤이 바닥에 남기는 지속 장판. 서 있는 동안 둔화(얼음 장판)나 도트 피해(2페이즈 산성 웅덩이)를 건다.
/// 원형 디스크 메시 + 테두리 선으로 그리며, 지속 시간이 끝나면 옅어지며 풀로 돌아간다.
/// 지속 시간이 0 이하면 ClearAll() 전까지 사라지지 않는다 (산성 웅덩이용).
///
/// 판정은 호스트/싱글 원본만 하고, 클라에는 NetworkMonsterDirector가 연출용 복제본(InitVisual)을 띄운다.
/// </summary>
public class DragonHazardZone : MonoBehaviour, IPoolable
{
    #region Inspector
    [Tooltip("바닥 디스크 머티리얼 (알파 블렌딩 Unlit)")]
    [SerializeField] private Material fillMaterial;
    [Tooltip("테두리 선 머티리얼 (알파 블렌딩 Unlit, 렌더 큐 3100)")]
    [SerializeField] private Material lineMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color fillColor = new Color(0.55f, 0.85f, 1.2f, 0.35f);
    [ColorUsage(true, true)] [SerializeField] private Color lineColor = new Color(0.7f, 1.1f, 1.8f, 0.9f);
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private float groundOffset = 0.06f;
    [Tooltip("판정 주기(초). 둔화는 이 주기보다 조금 길게 걸어 장판 위에 있는 동안 끊기지 않게 한다")]
    [SerializeField] private float tickInterval = 0.2f;
    [Tooltip("사라질 때 옅어지는 시간(초)")]
    [SerializeField] private float fadeOutTime = 0.5f;
    #endregion

    private const int Segments = 48;
    private const float DotTickInterval = 0.5f; // 플레이어 도트 틱 간격
    private static readonly List<DragonHazardZone> s_active = new List<DragonHazardZone>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private LineRenderer _line;
    private MaterialPropertyBlock _block;
    private Mesh _mesh;
    private readonly HashSet<Player> _inside = new HashSet<Player>();

    private GameObject owner;
    private DragonBoss ownerBoss;  // 부식 배율용
    private Vector3 center;
    private float radius;
    private float lifeTime;      // 0 이하면 무한
    private float slowMultiplier;
    private float dotPerSecond;
    private float elapsed;
    private float tickTimer;
    private float fadeTimer;
    private bool active;
    private bool fading;
    private bool visualOnly;
    private bool loopHooked;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();

        var disc = new GameObject("Disc");
        disc.transform.SetParent(transform, false);
        _meshFilter = disc.AddComponent<MeshFilter>();
        _meshRenderer = disc.AddComponent<MeshRenderer>();
        _meshRenderer.sharedMaterial = fillMaterial;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _mesh = BuildDisc();
        _meshFilter.sharedMesh = _mesh;

        var lineGo = new GameObject("Outline");
        lineGo.transform.SetParent(transform, false);
        _line = lineGo.AddComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = true;
        _line.positionCount = Segments;
        _line.material = lineMaterial;
        _line.widthMultiplier = lineWidth;
        _line.alignment = LineAlignment.TransformZ;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
        // 바닥에 눕힌 원 (로컬 XZ 평면, 반지름 1 → transform 스케일로 키운다)
        lineGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int i = 0; i < Segments; i++)
        {
            float a = Mathf.PI * 2f * i / Segments;
            _line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        SetVisible(false);
    }

    // 반지름 1짜리 바닥 원판 (로컬 XZ)
    private static Mesh BuildDisc()
    {
        var mesh = new Mesh { name = "DragonHazardDisc" };
        var verts = new Vector3[Segments + 1];
        var tris = new int[Segments * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < Segments; i++)
        {
            float a = Mathf.PI * 2f * i / Segments;
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i == Segments - 1 ? 1 : i + 2;
            tris[i * 3 + 2] = i + 1;
        }
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    #region Init
    /// <summary>
    /// 장판을 깐다 (호스트/싱글 원본). duration이 0 이하면 ClearAll() 전까지 유지된다.
    /// slowMultiplier가 1 미만이면 서 있는 동안 둔화, dotPerSecond가 0보다 크면 초당 피해.
    /// </summary>
    public void Init(GameObject owner, Vector3 center, float radius, float duration, float slowMultiplier, float dotPerSecond)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonZone(Main.Pool.GetAddress(gameObject), center, radius, duration);
#endif
        Begin(owner, center, radius, duration, slowMultiplier, dotPerSecond, visual: false);
    }

    /// <summary>멀티 클라 전용: 연출용 복제본. 판정하지 않는다.</summary>
    public void InitVisual(Vector3 center, float radius, float duration)
        => Begin(null, center, radius, duration, 1f, 0f, visual: true);

    private void Begin(GameObject owner, Vector3 center, float radius, float duration, float slowMultiplier,
                       float dotPerSecond, bool visual)
    {
        this.owner = owner;
        ownerBoss = owner != null ? owner.GetComponent<DragonBoss>() : null;
        this.center = center;
        this.radius = Mathf.Max(radius, 0.2f);
        lifeTime = duration;
        this.slowMultiplier = slowMultiplier;
        this.dotPerSecond = dotPerSecond;
        visualOnly = visual;
        elapsed = 0f;
        tickTimer = 0f;
        fading = false;
        active = true;

        transform.SetPositionAndRotation(center + Vector3.up * groundOffset, Quaternion.identity);
        transform.localScale = new Vector3(this.radius, 1f, this.radius);
        ApplyAlpha(1f);
        SetVisible(true);

        if (!s_active.Contains(this)) s_active.Add(this);
    }
    #endregion

    /// <summary>깔려 있는 장판을 모두 걷어낸다 (페이즈 전환·보스 사망 시).</summary>
    public static void ClearAll()
    {
#if PHOTON_FUSION
        // 호스트가 지우면 클라의 연출 복제본(영구 산성 웅덩이 포함)도 함께 지운다
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonZonesClear();
#endif
        ClearLocal();
    }

    /// <summary>이 피어의 장판만 걷어낸다 (클라가 호스트의 정리 RPC를 받았을 때).</summary>
    public static void ClearLocal()
    {
        for (int i = s_active.Count - 1; i >= 0; i--)
        {
            var zone = s_active[i];
            if (zone != null && zone.active) zone.BeginFade();
        }
    }

    #region IPoolable
    public void OnSpawn() => active = false;

    public void OnDespawn()
    {
        active = false;
        fading = false;
        _inside.Clear();
        s_active.Remove(this);
        SetVisible(false);
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
        s_active.Remove(this);
        if (_mesh != null) Destroy(_mesh);
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= HandleGameUpdate;
        loopHooked = false;
    }

    private void HandleGameUpdate(float deltaTime)
    {
        if (!active || !isActiveAndEnabled) return;

        if (fading)
        {
            fadeTimer -= deltaTime;
            ApplyAlpha(Mathf.Clamp01(fadeTimer / Mathf.Max(fadeOutTime, 0.01f)));
            if (fadeTimer <= 0f)
            {
                active = false;
                Extensions.Despawn(gameObject);
            }
            return;
        }

        elapsed += deltaTime;
        if (lifeTime > 0f && elapsed >= lifeTime)
        {
            BeginFade();
            return;
        }

        if (visualOnly) return;

        tickTimer -= deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = tickInterval;
        Tick();
    }

    private void Tick()
    {
        _inside.Clear();
        foreach (var col in Physics.OverlapSphere(center, radius + 1f, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive || !_inside.Add(player)) continue;

            Vector3 d = player.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) continue;

            if (slowMultiplier < 1f)
                player.ApplySlow(slowMultiplier, tickInterval + 0.15f);

            // 서 있는 동안 플레이어 도트를 계속 갱신한다. 플레이어 도트는 방어력을 무시하고 경직도 없어서
            // 작은 틱이 방어구에 다 깎이거나 매 틱 움찔하지 않는다. 벗어나면 한 틱 더 들어가고 끝난다.
            if (dotPerSecond > 0f)
            {
                int perTick = Mathf.Max(Mathf.RoundToInt(dotPerSecond * DotTickInterval), 1);
                if (ownerBoss != null) perTick = ownerBoss.ModifyOutgoingDamage(player, perTick);
                player.ApplyDot(perTick, DotTickInterval + 0.1f, DotTickInterval);
            }
        }
    }

    private void BeginFade()
    {
        if (fading) return;
        fading = true;
        fadeTimer = fadeOutTime;
    }

    private void ApplyAlpha(float alpha)
    {
        if (_meshRenderer != null)
        {
            Color c = fillColor;
            c.a *= alpha;
            _block.SetColor(BaseColorId, c);
            _meshRenderer.SetPropertyBlock(_block);
        }

        if (_line != null)
        {
            Color c = lineColor;
            c.a *= alpha;
            _line.startColor = _line.endColor = c;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_meshRenderer != null) _meshRenderer.enabled = visible;
        if (_line != null) _line.enabled = visible;
    }
}

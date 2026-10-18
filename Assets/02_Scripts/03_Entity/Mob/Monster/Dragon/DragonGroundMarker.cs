using UnityEngine;

/// <summary>예고 표식 색 구분. 네트워크로 byte로 보내므로 값을 바꾸지 말고 뒤에만 추가한다.</summary>
public enum DragonMarkerStyle : byte
{
    /// <summary>일반 공격 예고 (얼음탄 착탄 지점, 돌진 경로).</summary>
    Danger = 0,
    /// <summary>전멸기 예고 (서리 폭발 범위).</summary>
    Wipe = 1,
    /// <summary>안전지대 (기둥 주변).</summary>
    Safe = 2,
}

/// <summary>
/// 드래곤 패턴의 바닥 예고 표식. 원(착탄 지점·폭발 범위) 또는 직사각형(돌진 경로)을 LineRenderer로 그린다.
/// 바깥 테두리는 고정이고, 안쪽 채움(원은 반지름, 직선은 길이)이 예고 시간 동안 자라나 "언제 터지는지"를 보여준다.
/// 예고 시간이 끝나면 풀로 돌아간다. 판정은 하지 않는다(판정은 각 패턴이 직접 한다).
///
/// 멀티: 호스트 원본이 Init에서 클라에 복제본(InitVisual)을 띄운다. (MageStarLaser와 같은 방식)
/// </summary>
public class DragonGroundMarker : MonoBehaviour, IPoolable
{
    #region Inspector
    [Tooltip("선 머티리얼 (알파 블렌딩 Unlit, 렌더 큐 3100). MageStarLaser.mat 재사용")]
    [SerializeField] private Material lineMaterial;
    [ColorUsage(true, true)] [SerializeField] private Color dangerColor = new Color(0.55f, 0.9f, 1.6f, 0.9f);
    [ColorUsage(true, true)] [SerializeField] private Color wipeColor = new Color(1.6f, 0.35f, 0.35f, 0.9f);
    [ColorUsage(true, true)] [SerializeField] private Color safeColor = new Color(1.4f, 1.3f, 0.6f, 0.9f);
    [SerializeField] private float outlineWidth = 0.12f;
    [SerializeField] private float fillWidth = 0.08f;
    [Tooltip("바닥에서 띄우는 높이(m)")]
    [SerializeField] private float groundOffset = 0.1f;
    #endregion

    private const int CircleSegments = 64;

    private enum Shape { Circle, Line }

    private LineRenderer _outline;
    private LineRenderer _fill;
    private Shape shape;
    private Vector3 center;
    private Vector3 lineStart;
    private Vector3 lineEnd;
    private float size;       // 원: 반지름 / 직선: 폭
    private float duration;
    private float timer;
    private bool active;
    private bool loopHooked;

    private void Awake()
    {
        _outline = CreateLine("Outline", outlineWidth);
        _fill = CreateLine("Fill", fillWidth);
        SetVisible(false);
    }

    private LineRenderer CreateLine(string lineName, float width)
    {
        var go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = lineMaterial;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    #region Init
    /// <summary>원형 예고. 호스트/싱글 원본이 부른다.</summary>
    public void InitCircle(Vector3 center, float radius, float duration, DragonMarkerStyle style)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonMarker(
                Main.Pool.GetAddress(gameObject), (byte)Shape.Circle, center, center, radius, duration, (byte)style);
#endif
        Begin(Shape.Circle, center, center, radius, duration, style);
    }

    /// <summary>직사각형 경로 예고 (start → end, 전체 폭 width). 호스트/싱글 원본이 부른다.</summary>
    public void InitLine(Vector3 start, Vector3 end, float width, float duration, DragonMarkerStyle style)
    {
#if PHOTON_FUSION
        if (NetworkMonsterDirector.ShouldBroadcastEffects)
            NetworkMonsterDirector.Instance.BroadcastDragonMarker(
                Main.Pool.GetAddress(gameObject), (byte)Shape.Line, start, end, width, duration, (byte)style);
#endif
        Begin(Shape.Line, start, end, width, duration, style);
    }

    /// <summary>멀티 클라 전용: 호스트가 띄운 표식의 복제본.</summary>
    public void InitVisual(byte shapeId, Vector3 a, Vector3 b, float size, float duration, byte style)
        => Begin((Shape)shapeId, a, b, size, duration, (DragonMarkerStyle)style);

    private void Begin(Shape shape, Vector3 a, Vector3 b, float size, float duration, DragonMarkerStyle style)
    {
        this.shape = shape;
        center = a;
        lineStart = a;
        lineEnd = b;
        this.size = Mathf.Max(size, 0.1f);
        this.duration = Mathf.Max(duration, 0.05f);
        timer = 0f;
        active = true;

        Color color = style switch
        {
            DragonMarkerStyle.Wipe => wipeColor,
            DragonMarkerStyle.Safe => safeColor,
            _ => dangerColor,
        };
        _outline.startColor = _outline.endColor = color;
        _fill.startColor = _fill.endColor = color;

        if (shape == Shape.Circle) DrawCircle(_outline, center, this.size);
        else DrawRect(_outline, lineStart, lineEnd, this.size);

        DrawFill(0f);
        SetVisible(true);
    }
    #endregion

    #region IPoolable
    public void OnSpawn() => active = false;

    public void OnDespawn()
    {
        active = false;
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
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= HandleGameUpdate;
        loopHooked = false;
    }

    private void HandleGameUpdate(float deltaTime)
    {
        if (!active || !isActiveAndEnabled) return;

        timer += deltaTime;
        DrawFill(Mathf.Clamp01(timer / duration));

        if (timer < duration) return;
        active = false;
        Extensions.Despawn(gameObject);
    }

    #region Draw
    private void DrawFill(float progress)
    {
        if (shape == Shape.Circle)
        {
            DrawCircle(_fill, center, Mathf.Max(size * progress, 0.05f));
            return;
        }

        // 직선: 시작점에서 끝점 쪽으로 가운데 선이 자라난다
        _fill.positionCount = 2;
        _fill.loop = false;
        _fill.SetPosition(0, Lift(lineStart));
        _fill.SetPosition(1, Lift(Vector3.Lerp(lineStart, lineEnd, Mathf.Max(progress, 0.01f))));
    }

    private void DrawCircle(LineRenderer lr, Vector3 c, float radius)
    {
        lr.positionCount = CircleSegments;
        lr.loop = true;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = Mathf.PI * 2f * i / CircleSegments;
            lr.SetPosition(i, Lift(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius));
        }
    }

    private void DrawRect(LineRenderer lr, Vector3 start, Vector3 end, float width)
    {
        Vector3 dir = end - start;
        dir.y = 0f;
        Vector3 side = dir.sqrMagnitude < 0.0001f ? Vector3.right : Vector3.Cross(Vector3.up, dir.normalized);
        side *= width * 0.5f;

        lr.positionCount = 4;
        lr.loop = true;
        lr.SetPosition(0, Lift(start - side));
        lr.SetPosition(1, Lift(end - side));
        lr.SetPosition(2, Lift(end + side));
        lr.SetPosition(3, Lift(start + side));
    }

    private Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + groundOffset, p.z);

    private void SetVisible(bool visible)
    {
        if (_outline != null) _outline.enabled = visible;
        if (_fill != null) _fill.enabled = visible;
    }
    #endregion
}

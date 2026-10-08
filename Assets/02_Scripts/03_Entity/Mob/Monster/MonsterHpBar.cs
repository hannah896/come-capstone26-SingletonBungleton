using UnityEngine;

/// <summary>
/// 몬스터 머리 위에 띄우는 체력바.
///
/// PlayerNameTag처럼 캔버스를 쓰지 않는다. 배경/채움 SpriteRenderer 두 장을 런타임에 자식으로 만들어 붙이므로
/// 몬스터 프리팹을 건드릴 필요가 없다. Monster.Awake에서 자동으로 붙는다.
///
/// - 한 대라도 맞아 체력이 줄었을 때만 보이고, 사망하면 숨긴다
/// - 매 프레임 카메라와 같은 방향을 바라보게 회전한다 (빌보드)
/// - 체력 비율은 Monster.HpRatio에서 읽는다 (클라이언트는 호스트가 복제한 값)
/// </summary>
public class MonsterHpBar : MonoBehaviour
{
    #region Fields
    // 머리 꼭대기에서 체력바까지 추가 높이
    private const float HeadOffset = 0.4f;

    // 바 크기(m)
    private const float BarWidth = 1f;
    private const float BarHeight = 0.12f;

    // 테두리 두께(m)
    private const float BorderSize = 0.02f;

    // 이 거리보다 멀면 숨긴다
    private const float MaxVisibleDistance = 30f;

    // 채움 색이 따라가는 속도 (값이 클수록 즉시 반영)
    private const float FillLerpSpeed = 12f;

    private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color FillColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    private static Sprite s_whiteSprite;
    private static Sprite s_whiteSpriteLeft;

    private Monster _monster;
    private Transform _root;
    private SpriteRenderer _back;
    private SpriteRenderer _fill;
    private float _height = 2f;
    private bool _heightCached;
    private float _shownRatio = 1f;
    private Camera _camera;
    private bool _subscribed;
    #endregion

    /// <summary>
    /// 몬스터에 연결한다. 풀에서 재사용될 때마다 다시 호출해도 안전하다(체력바를 가득 찬 상태로 되돌린다).
    /// </summary>
    public void Bind(Monster monster)
    {
        _monster = monster;

        EnsureBar();
        _heightCached = false; // 스폰 직후엔 애니 포즈가 안 잡혀 있을 수 있어 처음 보여줄 때 잰다

        // 몬스터 프리팹 스케일과 상관없이 바 크기를 일정하게
        Vector3 lossy = transform.lossyScale;
        _root.localScale = new Vector3(1f / Mathf.Max(lossy.x, 0.0001f), 1f / Mathf.Max(lossy.y, 0.0001f), 1f / Mathf.Max(lossy.z, 0.0001f));
        _shownRatio = 1f;
        ApplyFill(1f);
        SetVisible(false);

        if (!_subscribed)
        {
            Main.Loop.OnLateUpdate += OnLateUpdate;
            _subscribed = true;
        }
    }

    // 체력바 오브젝트를 한 번만 생성
    private void EnsureBar()
    {
        if (_root != null) return;

        var root = new GameObject("HpBar");
        root.layer = 0; // Default — 1인칭 전용 레이어(ViewModel)에 올라가지 않도록
        root.transform.SetParent(transform, false);
        _root = root.transform;

        // 배경은 테두리 두께만큼 크게 깔고, 채움은 왼쪽 끝을 기준으로 가로 스케일만 줄인다
        _back = CreateQuad("Back", BackColor, 0, leftPivot: false, new Vector2(BarWidth + BorderSize * 2f, BarHeight + BorderSize * 2f));
        _back.transform.localPosition = Vector3.zero;

        _fill = CreateQuad("Fill", FillColor, 1, leftPivot: true, new Vector2(BarWidth, BarHeight));
        // 배경보다 살짝 카메라 쪽으로 (빌보드 회전이 카메라 회전과 같으므로 로컬 -Z가 카메라 쪽)
        _fill.transform.localPosition = new Vector3(-BarWidth * 0.5f, 0f, -0.001f);
    }

    private SpriteRenderer CreateQuad(string name, Color color, int sortingOrder, bool leftPivot, Vector2 size)
    {
        var go = new GameObject(name);
        go.layer = 0;
        go.transform.SetParent(_root, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite(leftPivot);
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return sr;
    }

    // 1x1 흰색 스프라이트 (1유닛 크기). 채움용은 왼쪽 가운데가 피벗이라 가로 스케일만 줄이면 오른쪽부터 깎인다.
    private static Sprite GetWhiteSprite(bool leftPivot)
    {
        if (leftPivot)
        {
            if (s_whiteSpriteLeft == null)
                s_whiteSpriteLeft = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            return s_whiteSpriteLeft;
        }

        if (s_whiteSprite == null)
            s_whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return s_whiteSprite;
    }

    // 몬스터 키 높이 계산. 콜라이더와 메시 중 더 높은 쪽을 쓴다.
    // (트리가드처럼 콜라이더를 몸통에만 맞춘 몬스터는 콜라이더 기준이면 바가 머리에 묻힌다)
    private void CacheHeight()
    {
        float top = float.MinValue;

        if (TryGetComponent(out Collider col) && col.enabled)
            top = col.bounds.max.y;

        // 이펙트(파티클 등)는 제외하고 모델 메시만 본다
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            if (_root != null && r.transform.IsChildOf(_root)) continue; // 체력바 자신은 제외
            top = Mathf.Max(top, r.bounds.max.y);
        }

        if (top > float.MinValue) _height = top - transform.position.y;
        _heightCached = true;
    }

    private void ApplyFill(float ratio)
    {
        if (_fill == null) return;
        Vector3 s = _fill.transform.localScale;
        s.x = BarWidth * Mathf.Clamp01(ratio);
        _fill.transform.localScale = s;
    }

    private void SetVisible(bool visible)
    {
        if (_back.enabled != visible) _back.enabled = visible;
        if (_fill.enabled != visible) _fill.enabled = visible;
    }

    private void OnLateUpdate(float deltaTime)
    {
        if (_root == null || _monster == null) return;

        if (!isActiveAndEnabled) return;

        float ratio = _monster.HpRatio;

        // 풀 피거나 사망했으면 숨김
        bool show = ratio > 0f && ratio < 0.999f && _monster.CurrentAnimId != MonsterAnimId.Dead;
        if (!show)
        {
            SetVisible(false);
            if (ratio >= 0.999f) _shownRatio = 1f;
            return;
        }

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        if (!_heightCached) CacheHeight();
        _root.position = transform.position + Vector3.up * (_height + HeadOffset);

        bool inRange = (_root.position - _camera.transform.position).sqrMagnitude <= MaxVisibleDistance * MaxVisibleDistance;
        SetVisible(inRange);
        if (!inRange) return;

        // 카메라와 같은 방향을 바라보게
        _root.rotation = _camera.transform.rotation;

        _shownRatio = Mathf.Lerp(_shownRatio, ratio, 1f - Mathf.Exp(-FillLerpSpeed * deltaTime));
        ApplyFill(_shownRatio);
    }

    private void OnDestroy()
    {
        if (_subscribed && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnLateUpdate -= OnLateUpdate;
    }
}

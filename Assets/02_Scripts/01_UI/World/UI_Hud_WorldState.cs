using UnityEngine;

/// <summary>
/// 월드 상태 HUD의 컨테이너다.
/// 하나의 월드맵을 HUD 슬롯과 전체 화면 사이에서 전환한다.
/// </summary>
public class UI_Hud_WorldState : UI_Hud
{
    [Header("World State Views-Top")]
    [SerializeField] private UI_Panel_WorldClock _worldClockView;
    [SerializeField] private UI_Panel_WorldMap _worldMapView;

    [Header("Map Presentation")]
    [SerializeField] private RectTransform _mapSlot;
    [SerializeField] private RectTransform _expandedMapRoot;

    [Header("Map Target")]
    [SerializeField] private Transform _target;

    [Header("World State Views-Bottom")]
    //[SerializeField] private UI_Panel_MoonSpiritFill _moonSpiritFill;
    //[SerializeField] private UI_Panel_MoonShape _moonShape;


    private float _nextTargetSearchTime;

    public UI_Panel_WorldClock WorldClockView => _worldClockView;
    public UI_Panel_WorldMap WorldMapView => _worldMapView;
    public bool IsMapExpanded { get; private set; }

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _worldClockView ??= GetComponentInChildren<UI_Panel_WorldClock>(true);
        _worldMapView ??= GetComponentInChildren<UI_Panel_WorldMap>(true);

        _worldClockView?.Initialize();
        _worldMapView?.Initialize();
        if (_target != null) _worldMapView?.SetTarget(_target);
        SetMapExpanded(false);
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        SetMapExpanded(false);
        RefreshTarget();
    }

    private void LateUpdate()
    {
        if (_worldMapView == null || _target != null) return;
        if (Time.unscaledTime < _nextTargetSearchTime) return;

        _nextTargetSearchTime = Time.unscaledTime + 1f;
        RefreshTarget();
    }

    /// <summary>지도의 플레이어 마커가 추적할 대상을 지정한다.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        _worldMapView?.SetTarget(_target);
    }

    public void ToggleMap() => SetMapExpanded(!IsMapExpanded);

    public void SetMapExpanded(bool expanded)
    {
        if (_worldMapView == null || _mapSlot == null || _expandedMapRoot == null) return;

        // 목적지를 먼저 활성화해야 재배치 중 OnDisable로 탐색 상태가 초기화되지 않는다.
        if (expanded)
        {
            _expandedMapRoot.gameObject.SetActive(true);
            // 프리팹 단독 편집 시 루트 Canvas로 저장되어도, 실행 중에는 HUD 위에 정렬한다.
            Canvas overlay = _expandedMapRoot.GetComponent<Canvas>();
            if (overlay != null) overlay.overrideSorting = true;
        }
        RectTransform mapRect = (RectTransform)_worldMapView.transform;
        mapRect.SetParent(expanded ? _expandedMapRoot : _mapSlot, false);
        mapRect.SetAsLastSibling();
        mapRect.localScale = Vector3.one;
        mapRect.localRotation = Quaternion.identity;
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.sizeDelta = Vector2.zero;
        mapRect.anchoredPosition3D = Vector3.zero;
        IsMapExpanded = expanded;
        _worldMapView.SetExpanded(expanded);
        if (!expanded) _expandedMapRoot.gameObject.SetActive(false);
    }

    private void RefreshTarget()
    {
        if (_target != null)
        {
            _worldMapView?.SetTarget(_target);
            return;
        }

        Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player player in players)
        {
            if (player != null && player.IsLocalPlayer)
            {
                SetTarget(player.transform);
                return;
            }
        }
    }
}

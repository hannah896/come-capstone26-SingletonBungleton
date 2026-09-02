using UnityEngine;

/// <summary>
/// 월드 상태 HUD의 컨테이너다.
/// 월드 데이터를 소유하지 않고, 시계와 미니맵 View를 초기화하고 미니맵의 추적 대상을 전달한다.
/// </summary>
public class UI_Hud_WorldState : UI_Hud
{
    [Header("World State Views-Top")]
    [SerializeField] private UI_Panel_WorldClock _worldClockView;
    [SerializeField] private UI_Panel_WorldMiniMap _worldMiniMapView;

    [Header("Mini Map Target")]
    [SerializeField] private Transform _target;

    [Header("World State Views-Bottom")]
    //[SerializeField] private UI_Panel_MoonSpiritFill _moonSpiritFill;
    //[SerializeField] private UI_Panel_MoonShape _moonShape;


    private float _nextTargetSearchTime;

    public UI_Panel_WorldClock WorldClockView => _worldClockView;
    public UI_Panel_WorldMiniMap WorldMiniMapView => _worldMiniMapView;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _worldClockView ??= GetComponentInChildren<UI_Panel_WorldClock>(true);
        _worldMiniMapView ??= GetComponentInChildren<UI_Panel_WorldMiniMap>(true);

        _worldClockView?.Initialize();
        _worldMiniMapView?.Initialize();
        _worldMiniMapView?.SetTarget(_target);
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        RefreshTarget();
    }

    private void LateUpdate()
    {
        if (_worldMiniMapView == null || _target != null) return;
        if (Time.unscaledTime < _nextTargetSearchTime) return;

        _nextTargetSearchTime = Time.unscaledTime + 1f;
        RefreshTarget();
    }

    /// <summary>미니맵 마커가 추적할 대상을 지정한다.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        _worldMiniMapView?.SetTarget(_target);
    }

    private void RefreshTarget()
    {
        if (_target != null)
        {
            _worldMiniMapView?.SetTarget(_target);
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

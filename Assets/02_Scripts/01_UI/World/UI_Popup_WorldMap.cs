using UnityEngine;

/// <summary>
/// <see cref="WorldMap"/>이 소유한 전체 지도 스프라이트를 표시하는 팝업 View다.
/// 지도 생성은 하지 않으며, 열려 있는 동안에만 모델을 구독한다.
/// </summary>
public class UI_Popup_WorldMap : UI_Popup
{
    [Header("UI References")]
    [SerializeField] private UI_Image _mapImage;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [SerializeField] private bool _rotatePlayerMarker = false;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;

    private WorldMap _worldMap;
    private WorldMapData _mapData;
    private Transform _target;
    private float _nextTargetSearchTime;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 배경 이미지와 지도 이미지를 혼동하지 않도록, 전체 지도용 UI_Image는 프리팹에서 명시적으로 연결한다.
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
        TryFindLocalPlayer();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void LateUpdate()
    {
        if (_worldMap == null) TryBind();

        if (_target == null && Time.unscaledTime >= _nextTargetSearchTime)
        {
            _nextTargetSearchTime = Time.unscaledTime + 1f;
            TryFindLocalPlayer();
        }

        UpdatePlayerMarker();
    }

    /// <summary>외부에서 추적 대상을 명시적으로 지정할 때 사용한다.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        RefreshVisibility();
        UpdatePlayerMarker();
    }

    public void Bind(WorldMap worldMap)
    {
        if (_worldMap == worldMap)
        {
            RefreshFromModel();
            return;
        }

        Unbind();
        if (worldMap == null) return;

        _worldMap = worldMap;
        _worldMap.OnDataChanged += HandleDataChanged;
        _worldMap.OnDataCleared += HandleDataCleared;
        RefreshFromModel();
    }

    public void Unbind()
    {
        if (_worldMap != null)
        {
            _worldMap.OnDataChanged -= HandleDataChanged;
            _worldMap.OnDataCleared -= HandleDataCleared;
            _worldMap = null;
        }

        ClearView();
    }

    private void TryBind()
    {
        if (WorldMap.Instance != null) Bind(WorldMap.Instance);
    }

    private void RefreshFromModel()
    {
        if (_worldMap?.CurrentData != null) HandleDataChanged(_worldMap.CurrentData);
        else ClearView();
    }

    private void HandleDataChanged(WorldMapData data)
    {
        _mapData = data;
        if (_mapImage != null) _mapImage.Sprite = _mapData?.Sprite;
        RefreshVisibility();
        UpdatePlayerMarker();
    }

    private void HandleDataCleared()
    {
        ClearView();
    }

    private void ClearView()
    {
        _mapData = null;
        if (_mapImage != null) _mapImage.Sprite = null;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_mapImage != null) _mapImage.gameObject.SetActive(_mapData?.Sprite != null);
        if (_playerMarker != null) _playerMarker.gameObject.SetActive(_mapData != null && _target != null);
    }

    private void TryFindLocalPlayer()
    {
        foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player != null && player.IsLocalPlayer)
            {
                SetTarget(player.transform);
                return;
            }
        }
    }

    private void UpdatePlayerMarker()
    {
        if (_mapData == null || _target == null || _playerMarker == null) return;

        Vector3 position = _target.position;
        bool outsideWorld = position.x < 0f || position.z < 0f
            || position.x > _mapData.TerrainSize.x || position.z > _mapData.TerrainSize.y;
        if (_hideMarkerOutsideWorld && outsideWorld)
        {
            if (_playerMarker.gameObject.activeSelf) _playerMarker.gameObject.SetActive(false);
            return;
        }

        if (!_playerMarker.gameObject.activeSelf) _playerMarker.gameObject.SetActive(true);

        Vector2 normalized = _mapData.NormalizeWorldPosition(position);
        _playerMarker.anchorMin = normalized;
        _playerMarker.anchorMax = normalized;
        _playerMarker.anchoredPosition = Vector2.zero;
        if (_rotatePlayerMarker)
        {
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_target.eulerAngles.y);
        }
    }
}

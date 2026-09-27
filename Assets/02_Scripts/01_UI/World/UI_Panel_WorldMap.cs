using UnityEngine;

/// <summary>
/// <see cref="WorldMap"/>의 전체 지도를 HUD와 확대 화면에서 공유하는 View다.
/// 표시 크기를 바꿔도 같은 지도, 탐색 상태와 플레이어 마커를 유지한다.
/// </summary>
public class UI_Panel_WorldMap : UI_Panel
{
    [Header("UI References")]
    [SerializeField] private UI_Image _mapImage;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [Tooltip("Main Camera의 수평 시야 방향에 맞춰 마커와 시야선을 함께 회전한다.")]
    [SerializeField] private bool _rotatePlayerMarker = true;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;
    [SerializeField, Min(1f)] private float _compactMarkerSize = 15f;
    [SerializeField, Min(1f)] private float _expandedMarkerSize = 50f;

    [Header("Viewport Settings")]
    [Tooltip("인게임 진입 및 더블클릭 시 플레이어 주변을 보여 주는 기본 확대 배율이다.")]
    [SerializeField, Min(1f)] private float _zoom = 4f;
    [SerializeField, Min(1f)] private float _maxZoom = 16f;
    [Tooltip("휠 한 칸마다 변경할 배율의 비율. 0.2이면 20%씩 확대한다.")]
    [SerializeField, Min(0.01f)] private float _zoomStep = 0.2f;

    private UI_MapViewport _navigation;
    private WorldMap _worldMap;
    private WorldMapData _mapData;
    private Transform _target;
    private float _nextTargetSearchTime;

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 배경 이미지와 지도 이미지를 혼동하지 않도록, 전체 지도용 UI_Image는 프리팹에서 명시적으로 연결한다.
        _navigation = UI_MapViewport.Create(_mapImage, null, _playerMarker,
            _zoom, _maxZoom, _zoomStep, fillViewport: false, followTarget: true);
        if (_navigation != null) _mapImage = _navigation.MapImage;
        SetExpanded(false);
        return true;
    }

    /// <summary>지도 배율은 유지하고 표시 모드에 맞는 마커 크기만 적용한다.</summary>
    public void SetExpanded(bool expanded)
    {
        if (_playerMarker != null)
        {
            _playerMarker.localScale = Vector3.one;
            _playerMarker.sizeDelta = Vector2.one * (expanded ? _expandedMarkerSize : _compactMarkerSize);
        }
        _navigation?.CancelDrag();
        _navigation?.Refresh();
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
        TryFindLocalPlayer();
        // Cinemachine이 카메라 회전을 마친 뒤, UI를 그리기 직전에 방향을 반영한다.
        Canvas.willRenderCanvases += UpdatePlayerMarkerRotation;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdatePlayerMarkerRotation;
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
        bool targetChanged = _target != target;
        _target = target;
        RefreshVisibility();
        UpdatePlayerMarker();
        if (targetChanged) _navigation?.ResetView();
    }

    /// <summary>현재 플레이어 위치를 중앙에 놓고 기본 확대 배율과 위치 추적을 복구한다.</summary>
    public void ResetToDefault()
    {
        UpdatePlayerMarker();
        _navigation?.ResetView();
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
        bool mapChanged = !ReferenceEquals(_mapData, data);
        _mapData = data;
        if (_mapImage != null) _mapImage.Sprite = _mapData?.Sprite;
        RefreshVisibility();
        UpdatePlayerMarker();
        if (mapChanged) _navigation?.ResetView();
    }

    private void HandleDataCleared()
    {
        ClearView();
    }

    private void ClearView()
    {
        _mapData = null;
        if (_mapImage != null) _mapImage.Sprite = null;
        _navigation?.ResetView();
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_mapImage != null) _mapImage.Image.enabled = _mapData?.Sprite != null;
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
        if (_navigation != null)
        {
            _navigation.FocusPosition = _mapData != null && _target != null
                ? _mapData.NormalizeWorldPosition(_target.position)
                : new Vector2(0.5f, 0.5f);
            _navigation.Refresh();
        }
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
        _navigation?.SetMarkerPosition(normalized);
        UpdatePlayerMarkerRotation();
    }

    private void UpdatePlayerMarkerRotation()
    {
        if (!_rotatePlayerMarker || !isActiveAndEnabled || _target == null
            || _playerMarker == null || !_playerMarker.gameObject.activeInHierarchy) return;

        Camera viewCamera = Camera.main;
        Vector3 forward = viewCamera != null ? viewCamera.transform.forward : _target.forward;
        // 지도 위쪽은 월드 +Z다. 상하 시선은 제외하고 수직을 바라볼 때는 마지막 방향을 유지한다.
        Vector2 heading = new Vector2(forward.x, forward.z);
        if (heading.sqrMagnitude < 0.000001f) return;

        float yaw = Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
        _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }
}

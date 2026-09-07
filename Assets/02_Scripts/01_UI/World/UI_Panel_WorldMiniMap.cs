using UnityEngine;

/// <summary>
/// <see cref="WorldMap"/>의 데이터를 표시하는 HUD View다.
/// 텍스처 생성과 소유는 WorldMap이 담당하며, 이 클래스는 표시와 플레이어 마커만 담당한다.
/// </summary>
public class UI_Panel_WorldMiniMap : UI_Panel
{
    [Header("UI References")]
    [Tooltip("BG와 분리된 MapImage를 연결한다. 이 이미지의 RectTransform이 지도 표시 영역이 된다.")]
    [SerializeField] private UI_Image _mapImage;
    [Tooltip("별도의 표시 영역이 있을 때만 지정한다. 비워 두면 MapImage의 영역을 그대로 사용한다.")]
    [SerializeField] private RectTransform _mapViewport;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [SerializeField] private bool _rotatePlayerMarker = false;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;

    [Header("Viewport Settings")]
    [Tooltip("Viewport를 쓸 때 화면에 보이는 지도 배율이다. 4이면 가로·세로가 대략 1/4씩 보인다.")]
    [SerializeField, Min(1f)] private float _zoom = 4f;
    [SerializeField, Min(1f)] private float _maxZoom = 16f;
    [Tooltip("휠 한 칸마다 변경할 배율의 비율. 0.2이면 20%씩 확대한다.")]
    [SerializeField, Min(0.01f)] private float _zoomStep = 0.2f;

    private UI_MapViewport _navigation;
    private WorldMap _worldMap;
    private WorldMapData _mapData;
    private Transform _target;

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        // 프리팹 참조가 비어 있어도 이름이 명확한 지도와 마커만 찾고 BG는 건드리지 않는다.
        if (_mapImage == null)
            _mapImage = transform.Find("MapImage")?.GetComponent<UI_Image>();
        if (_playerMarker == null && _mapImage != null)
            _playerMarker = _mapImage.transform.Find("PlayerMarker") as RectTransform;

        _navigation = UI_MapViewport.Create(_mapImage, _mapViewport, _playerMarker,
            _zoom, _maxZoom, _zoomStep, fillViewport: true, followTarget: true);
        if (_navigation != null)
        {
            _mapImage = _navigation.MapImage;
            _mapViewport = _navigation.Viewport;
        }
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void LateUpdate()
    {
        if (_worldMap == null)
        {
            TryBind();
        }

        UpdatePlayerMarker();
    }

    /// <summary>미니맵 마커가 추적할 대상을 지정한다.</summary>
    public void SetTarget(Transform target)
    {
        _target = target;
        RefreshVisibility();
        UpdatePlayerMarker();
    }

    /// <summary>표시할 미니맵 서비스를 바인딩한다.</summary>
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

    /// <summary>현재 바인딩을 해제한다.</summary>
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
        if (WorldMap.Instance != null)
        {
            Bind(WorldMap.Instance);
        }
    }

    private void RefreshFromModel()
    {
        if (_worldMap?.CurrentData != null)
        {
            HandleDataChanged(_worldMap.CurrentData);
        }
        else
        {
            ClearView();
        }
    }

    private void HandleDataChanged(WorldMapData data)
    {
        _mapData = data;

        if (_mapImage != null)
        {
            _mapImage.Sprite = _mapData?.Sprite;
        }

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

        if (_mapImage != null)
        {
            _mapImage.Sprite = null;
        }

        _navigation?.ResetView();
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_mapImage != null)
        {
            _mapImage.Image.enabled = _mapData?.Sprite != null;
        }

        if (_playerMarker != null)
        {
            _playerMarker.gameObject.SetActive(_mapData != null && _target != null);
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

        Vector3 worldPosition = _target.position;
        bool isOutsideWorld = worldPosition.x < 0f || worldPosition.z < 0f
            || worldPosition.x > _mapData.TerrainSize.x || worldPosition.z > _mapData.TerrainSize.y;

        if (_hideMarkerOutsideWorld && isOutsideWorld)
        {
            if (_playerMarker.gameObject.activeSelf)
            {
                _playerMarker.gameObject.SetActive(false);
            }
            return;
        }

        if (!_playerMarker.gameObject.activeSelf)
        {
            _playerMarker.gameObject.SetActive(true);
        }

        Vector2 normalizedPosition = _mapData.NormalizeWorldPosition(worldPosition);
        _navigation?.SetMarkerPosition(normalizedPosition);

        if (_rotatePlayerMarker)
        {
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_target.eulerAngles.y);
        }
    }

}

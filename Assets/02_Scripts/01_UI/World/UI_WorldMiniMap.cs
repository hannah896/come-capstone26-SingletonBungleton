using UnityEngine;

/// <summary>
/// <see cref="WorldMap"/>의 데이터를 표시하는 HUD View다.
/// 텍스처 생성과 소유는 WorldMap이 담당하며, 이 클래스는 표시와 플레이어 마커만 담당한다.
/// </summary>
public class UI_WorldMiniMap : UI
{
    [Header("UI References")]
    [Tooltip("런타임 미니맵 Sprite를 표시할 UI_Image입니다. 프레임 이미지는 별도 오브젝트로 두세요.")]
    [SerializeField] private UI_Image _mapImage;
    [Tooltip("RectMask2D가 붙은 표시 영역. 비워 두면 기존처럼 전체 지도를 축소 표시한다.")]
    [SerializeField] private RectTransform _mapViewport;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [SerializeField] private bool _rotatePlayerMarker = false;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;

    [Header("Viewport Settings")]
    [Tooltip("Viewport를 쓸 때 화면에 보이는 지도 배율이다. 4이면 가로·세로가 대략 1/4씩 보인다.")]
    [SerializeField, Min(1f)] private float _zoom = 4f;

    private WorldMap _worldMap;
    private WorldMapData _mapData;
    private Transform _target;

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 기존 HUD 프리팹은 UI_WorldMiniMap 자신이 UI_Image인 구조다.
        if (_mapImage == null && _mapViewport == null)
        {
            _mapImage = GetComponent<UI_Image>();
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
        UpdateMapContent();
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
        UpdateMapContent();
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

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_mapImage != null)
        {
            _mapImage.enabled = _mapData?.Sprite != null;
        }

        if (_playerMarker != null)
        {
            _playerMarker.gameObject.SetActive(_mapData != null && _target != null);
        }
    }

    private void UpdatePlayerMarker()
    {
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
        UpdateMapContent(normalizedPosition);

        Vector2 markerPosition = _mapViewport != null ? new Vector2(0.5f, 0.5f) : normalizedPosition;
        _playerMarker.anchorMin = markerPosition;
        _playerMarker.anchorMax = markerPosition;
        _playerMarker.anchoredPosition = Vector2.zero;

        if (_rotatePlayerMarker)
        {
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_target.eulerAngles.y);
        }
    }

    private void UpdateMapContent()
    {
        if (_mapData == null || _mapViewport == null || _mapImage == null) return;
        UpdateMapContent(new Vector2(0.5f, 0.5f));
    }

    private void UpdateMapContent(Vector2 normalizedPlayerPosition)
    {
        if (_mapData == null || _mapViewport == null || _mapImage == null) return;

        RectTransform mapContent = _mapImage.Rect;
        Vector2 viewportSize = _mapViewport.rect.size;
        if (viewportSize.x <= 0f || viewportSize.y <= 0f) return;

        float mapAspect = _mapData.Sprite.rect.width / _mapData.Sprite.rect.height;
        float viewportAspect = viewportSize.x / viewportSize.y;
        float zoom = Mathf.Max(1f, _zoom);
        Vector2 contentSize;
        if (mapAspect >= viewportAspect)
        {
            contentSize = new Vector2(viewportSize.y * zoom * mapAspect, viewportSize.y * zoom);
        }
        else
        {
            contentSize = new Vector2(viewportSize.x * zoom, viewportSize.x * zoom / mapAspect);
        }

        mapContent.anchorMin = new Vector2(0.5f, 0.5f);
        mapContent.anchorMax = new Vector2(0.5f, 0.5f);
        mapContent.pivot = new Vector2(0.5f, 0.5f);
        mapContent.sizeDelta = contentSize;
        mapContent.anchoredPosition = new Vector2(
            (0.5f - normalizedPlayerPosition.x) * contentSize.x,
            (0.5f - normalizedPlayerPosition.y) * contentSize.y);
    }
}

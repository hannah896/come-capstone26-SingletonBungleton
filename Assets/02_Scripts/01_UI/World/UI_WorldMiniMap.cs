using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="WorldMiniMap"/>의 데이터를 표시하는 View다.
/// 텍스처 생성과 소유는 WorldMiniMap이 담당하며, 이 클래스는 표시와 플레이어 마커만 담당한다.
/// </summary>
public class UI_WorldMiniMap : UI
{
    [Header("UI References")]
    [SerializeField] private RawImage _mapImage;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [SerializeField] private bool _rotatePlayerMarker = true;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;

    private WorldMiniMap _worldMiniMap;
    private WorldMiniMapData _mapData;
    private Transform _target;

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _mapImage ??= GetComponentInChildren<RawImage>(true);
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
        if (_worldMiniMap == null)
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
    public void Bind(WorldMiniMap worldMiniMap)
    {
        if (_worldMiniMap == worldMiniMap)
        {
            RefreshFromModel();
            return;
        }

        Unbind();
        if (worldMiniMap == null) return;

        _worldMiniMap = worldMiniMap;
        _worldMiniMap.OnDataChanged += HandleDataChanged;
        _worldMiniMap.OnDataCleared += HandleDataCleared;
        RefreshFromModel();
    }

    /// <summary>현재 바인딩을 해제한다.</summary>
    public void Unbind()
    {
        if (_worldMiniMap != null)
        {
            _worldMiniMap.OnDataChanged -= HandleDataChanged;
            _worldMiniMap.OnDataCleared -= HandleDataCleared;
            _worldMiniMap = null;
        }

        ClearView();
    }

    private void TryBind()
    {
        if (WorldMiniMap.Instance != null)
        {
            Bind(WorldMiniMap.Instance);
        }
    }

    private void RefreshFromModel()
    {
        if (_worldMiniMap?.CurrentData != null)
        {
            HandleDataChanged(_worldMiniMap.CurrentData);
        }
        else
        {
            ClearView();
        }
    }

    private void HandleDataChanged(WorldMiniMapData data)
    {
        _mapData = data;

        if (_mapImage != null)
        {
            _mapImage.texture = _mapData?.Texture;
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
            _mapImage.texture = null;
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_mapImage != null)
        {
            _mapImage.enabled = _mapData?.Texture != null;
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
        _playerMarker.anchorMin = normalizedPosition;
        _playerMarker.anchorMax = normalizedPosition;
        _playerMarker.anchoredPosition = Vector2.zero;

        if (_rotatePlayerMarker)
        {
            _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -_target.eulerAngles.y);
        }
    }
}

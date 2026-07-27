using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WorldMiniMap 데이터를 표시하는 HUD View.
/// 월드 데이터의 생성과 소유는 WorldMiniMap이 담당하고, 이 클래스는 구독·표시·해제만 담당한다.
/// </summary>
public class UI_Hud_WorldMiniMap : UI_Hud
{
    #region Fields

    [Header("UI References")]
    [SerializeField] private RawImage _mapImage;
    [SerializeField] private RectTransform _playerMarker;

    [Header("Marker Settings")]
    [SerializeField] private bool _rotatePlayerMarker = true;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;
    
    private WorldMiniMap _worldMiniMap;
    private WorldMiniMapData _mapData;

    private Transform _target;

    #endregion

    #region Properties

    public Transform Target
    {
        get => _target;
        set
        {
            _target = value;
            RefreshVisibility();
            UpdatePlayerMarker();
        }
    }

    #endregion

    #region Initialize / Bind

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        ResolveReferences();
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        Bind(WorldMiniMap.Instance);
    }

    private void OnDisable()
    {
        Unbind();
    }

    /// <summary>외부 생성 순서가 HUD보다 늦는 경우에 대비한 명시적 바인드 진입점.</summary>
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

    #endregion

    #region Event Handlers

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

    #endregion

    #region View Update

    private void LateUpdate()
    {
        // HUD가 먼저 생성된 경우에도 월드 서비스가 준비되는 즉시 구독한다.
        if (_worldMiniMap == null && WorldMiniMap.Instance != null)
        {
            Bind(WorldMiniMap.Instance);
        }

        UpdatePlayerMarker();
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

    private void ClearView()
    {
        _mapData = null;

        if (_mapImage != null)
        {
            _mapImage.texture = null;
        }

        RefreshVisibility();
    }

    private void ResolveReferences()
    {
        _mapImage ??= GetComponentInChildren<RawImage>(true);

        if (_playerMarker == null)
        {
            Transform marker = transform.Find("PlayerMarker");
            _playerMarker = marker as RectTransform;
        }
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

    #endregion
}

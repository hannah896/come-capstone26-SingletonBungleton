using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="WorldMap"/>의 전체 지도를 HUD와 확대 화면에서 공유하는 View다.
/// 표시 크기를 바꿔도 같은 지도, 탐색 상태와 플레이어 마커를 유지한다.
/// </summary>
public class UI_Panel_WorldMap : UI_Panel
{
    [Header("UI References")]
    [SerializeField] private UI_Image _mapImage;
    [SerializeField] private RectTransform _playerMarker;
    [Tooltip("로컬 인디케이터의 Marker 이미지만 연결한다. 원격 플레이어에게는 시야선을 복제하지 않는다.")]
    [SerializeField] private RectTransform _remoteMarkerTemplate;

    [Header("Marker Settings")]
    [Tooltip("Main Camera의 수평 시야 방향에 맞춰 마커와 시야선을 함께 회전한다.")]
    [SerializeField] private bool _rotatePlayerMarker = true;
    [SerializeField] private bool _hideMarkerOutsideWorld = true;
    [SerializeField, Min(1f)] private float _compactMarkerSize = 15f;
    [SerializeField, Min(1f)] private float _expandedMarkerSize = 50f;

    [Header("Resource Node Icons")]
    [SerializeField] private Sprite _branchIcon;
    [SerializeField] private Sprite _bushIcon;
    [SerializeField] private Sprite _firIcon;
    [SerializeField] private Sprite _grassIcon;
    [SerializeField] private Sprite _rockIcon;
    [SerializeField, Min(1f)] private float _compactResourceIconSize = 16f;
    [SerializeField, Min(1f)] private float _expandedResourceIconSize = 30f;

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
    private float _nextRemoteSearchTime;
    private float _nextResourceRefreshTime;
    private bool _isExpanded;
    private readonly Dictionary<Transform, RectTransform> _remoteMarkers = new();
    private readonly HashSet<Transform> _remoteTargets = new();
    private readonly List<Transform> _removedTargets = new();
    private readonly Dictionary<DisposeData, RectTransform> _resourceMarkers = new();
    private readonly HashSet<DisposeData> _visibleResources = new();
    private readonly List<DisposeData> _removedResources = new();
    private readonly List<DisposeData> _orderedResources = new();

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        if (_remoteMarkerTemplate == null && _playerMarker != null)
            _remoteMarkerTemplate = _playerMarker.Find("Marker") as RectTransform;

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
        _isExpanded = expanded;
        float markerSize = expanded ? _expandedMarkerSize : _compactMarkerSize;
        if (_playerMarker != null)
        {
            _playerMarker.localScale = Vector3.one;
            _playerMarker.sizeDelta = Vector2.one * markerSize;
        }
        foreach (RectTransform marker in _remoteMarkers.Values)
            if (marker != null) marker.sizeDelta = Vector2.one * markerSize;
        float resourceSize = expanded ? _expandedResourceIconSize : _compactResourceIconSize;
        foreach (RectTransform marker in _resourceMarkers.Values)
            if (marker != null) marker.sizeDelta = Vector2.one * resourceSize;
        _navigation?.CancelDrag();
        _navigation?.Refresh();
        UpdateRemotePlayerMarkers();
        RefreshResourceMarkers();
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
        TryFindLocalPlayer();
        _nextRemoteSearchTime = 0f;
        // Cinemachine이 카메라 회전을 마친 뒤, UI를 그리기 직전에 방향을 반영한다.
        Canvas.willRenderCanvases += UpdatePlayerMarkerRotation;
        Canvas.willRenderCanvases += UpdateRemotePlayerMarkers;
        Canvas.willRenderCanvases += UpdateResourceMarkerPositions;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= UpdatePlayerMarkerRotation;
        Canvas.willRenderCanvases -= UpdateRemotePlayerMarkers;
        Canvas.willRenderCanvases -= UpdateResourceMarkerPositions;
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
        // 참가자 검색만 간격을 두고 수행하고, 이미 찾은 캐릭터의 위치는 매 프레임 갱신한다.
        if (Time.unscaledTime >= _nextRemoteSearchTime)
        {
            _nextRemoteSearchTime = Time.unscaledTime + 0.5f;
            RefreshRemotePlayers();
        }
        UpdateRemotePlayerMarkers();
        if (Time.unscaledTime >= _nextResourceRefreshTime)
        {
            _nextResourceRefreshTime = Time.unscaledTime + 0.25f;
            RefreshResourceMarkers();
        }
        UpdateResourceMarkerPositions();
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
        if (mapChanged) _nextRemoteSearchTime = 0f;
        if (mapChanged) ClearResourceMarkers();
        RefreshResourceMarkers();
    }

    private void HandleDataCleared()
    {
        ClearView();
    }

    private void ClearView()
    {
        ClearRemoteMarkers();
        ClearResourceMarkers();
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

    private void RefreshRemotePlayers()
    {
        if (_mapData?.Sprite == null || _navigation == null || _remoteMarkerTemplate == null) return;

        _remoteTargets.Clear();
        foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (!player.isActiveAndEnabled || player.IsLocalPlayer || player.transform == _target) continue;
            _remoteTargets.Add(player.transform);
            AddRemoteMarker(player.transform);
        }

        _removedTargets.Clear();
        foreach (Transform target in _remoteMarkers.Keys)
            if (!_remoteTargets.Contains(target)) _removedTargets.Add(target);
        foreach (Transform target in _removedTargets) RemoveRemoteMarker(target);
    }

    private void AddRemoteMarker(Transform target)
    {
        if (target == null || target == _target || _remoteMarkers.ContainsKey(target)
            || _navigation == null || _playerMarker == null || _remoteMarkerTemplate == null) return;

        var marker = new GameObject("RemotePlayerMarker", typeof(RectTransform)).GetComponent<RectTransform>();
        marker.gameObject.layer = _playerMarker.gameObject.layer;
        marker.SetParent(_navigation.Viewport, false);
        marker.anchorMin = marker.anchorMax = Vector2.one * 0.5f;
        marker.sizeDelta = _playerMarker.sizeDelta;
        // 별도의 루트 아래 이미지 부분만 재사용하여 SightArea와 로컬 시야 회전을 제외한다.
        Instantiate(_remoteMarkerTemplate, marker, false).gameObject.SetActive(true);
        foreach (Graphic graphic in marker.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        marker.SetSiblingIndex(_playerMarker.GetSiblingIndex());
        _remoteMarkers.Add(target, marker);
    }

    private void UpdateRemotePlayerMarkers()
    {
        if (_mapData?.Sprite == null || _navigation == null) return;

        _removedTargets.Clear();
        foreach (var pair in _remoteMarkers)
        {
            Transform target = pair.Key;
            RectTransform marker = pair.Value;
            if (target == null || !target.gameObject.activeInHierarchy || target == _target || marker == null)
            {
                _removedTargets.Add(target);
                continue;
            }

            Vector3 position = target.position;
            bool outsideWorld = position.x < 0f || position.z < 0f
                || position.x > _mapData.TerrainSize.x || position.z > _mapData.TerrainSize.y;
            bool visible = !_hideMarkerOutsideWorld || !outsideWorld;
            if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
            if (visible)
                marker.anchoredPosition = _navigation.NormalizedToViewportPosition(
                    _mapData.NormalizeWorldPosition(position));
        }
        foreach (Transform target in _removedTargets) RemoveRemoteMarker(target);
    }

    private void RemoveRemoteMarker(Transform target)
    {
        if (!_remoteMarkers.Remove(target, out RectTransform marker) || marker == null) return;
        marker.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(marker.gameObject);
        else DestroyImmediate(marker.gameObject);
    }

    private void ClearRemoteMarkers()
    {
        _removedTargets.Clear();
        _removedTargets.AddRange(_remoteMarkers.Keys);
        foreach (Transform target in _removedTargets) RemoveRemoteMarker(target);
        _removedTargets.Clear();
        _remoteTargets.Clear();
        _nextRemoteSearchTime = 0f;
    }

    private void RefreshResourceMarkers()
    {
        if (_mapData?.Sprite == null || _mapData.LogicData == null || _navigation == null) return;

        _navigation.Refresh();
        Rect visible = _navigation.VisibleNormalizedRect;
        float minX = visible.xMin * _mapData.TerrainSize.x;
        float maxX = visible.xMax * _mapData.TerrainSize.x;
        float minZ = visible.yMin * _mapData.TerrainSize.y;
        float maxZ = visible.yMax * _mapData.TerrainSize.y;
        int chunkSize = _mapData.LogicData.ChunkSize;
        bool markersChanged = false;
        _visibleResources.Clear();
        foreach (ChunkData chunk in _mapData.LogicData.GetAllChunks())
        {
            if (chunk?.DisposeDatas == null) continue;
            float chunkX = chunk.ChunkCoord.x * chunkSize;
            float chunkZ = chunk.ChunkCoord.y * chunkSize;
            if (chunkX > maxX || chunkX + chunkSize < minX
                || chunkZ > maxZ || chunkZ + chunkSize < minZ) continue;
            foreach (DisposeData dispose in chunk.DisposeDatas)
            {
                if (dispose == null || chunk.IsObjectDestroyed(dispose.instanceId)) continue;
                Sprite icon = GetResourceIcon(dispose.prefabName);
                if (icon == null) continue;

                Vector3 worldPosition = new Vector3(dispose.tilePosition.x + dispose.localOffset.x,
                    0f, dispose.tilePosition.y + dispose.localOffset.y);
                if (worldPosition.x < 0f || worldPosition.z < 0f || worldPosition.x > _mapData.TerrainSize.x
                    || worldPosition.z > _mapData.TerrainSize.y) continue;
                Vector2 normalized = _mapData.NormalizeWorldPosition(worldPosition);
                if (normalized.x < visible.xMin || normalized.x > visible.xMax
                    || normalized.y < visible.yMin || normalized.y > visible.yMax) continue;

                _visibleResources.Add(dispose);
                if (_resourceMarkers.ContainsKey(dispose)) continue;
                AddResourceMarker(dispose, icon);
                markersChanged = true;
            }
        }

        _removedResources.Clear();
        foreach (DisposeData dispose in _resourceMarkers.Keys)
            if (!_visibleResources.Contains(dispose)) _removedResources.Add(dispose);
        foreach (DisposeData dispose in _removedResources) RemoveResourceMarker(dispose);
        if (_removedResources.Count > 0) markersChanged = true;
        _removedResources.Clear();
        if (markersChanged) OrderResourceMarkers();
        UpdateResourceMarkerPositions();
    }

    private void OrderResourceMarkers()
    {
        _orderedResources.Clear();
        _orderedResources.AddRange(_resourceMarkers.Keys);
        _orderedResources.Sort(CompareResourcePositions);

        // 같은 지도에서 월드 +X가 화면 오른쪽이다. 오른쪽 아이콘을 나중에 그려 겹침 순서를 고정한다.
        int firstIndex = _mapImage.transform.GetSiblingIndex() + 1;
        for (int i = 0; i < _orderedResources.Count; i++)
            _resourceMarkers[_orderedResources[i]].SetSiblingIndex(firstIndex + i);
    }

    private static int CompareResourcePositions(DisposeData left, DisposeData right)
    {
        int x = (left.tilePosition.x + left.localOffset.x)
            .CompareTo(right.tilePosition.x + right.localOffset.x);
        if (x != 0) return x;

        int z = (left.tilePosition.y + left.localOffset.y)
            .CompareTo(right.tilePosition.y + right.localOffset.y);
        if (z != 0) return z;

        int id = left.instanceId.CompareTo(right.instanceId);
        return id != 0 ? id : string.CompareOrdinal(left.prefabName, right.prefabName);
    }

    private Sprite GetResourceIcon(string prefabName) => prefabName switch
    {
        "ResourceNode_Branch" => _branchIcon,
        "ResourceNode_Bush" => _bushIcon,
        "ResourceNode_Fir" => _firIcon,
        "ResourceNode_Grass" => _grassIcon,
        "ResourceNode_Rock" => _rockIcon,
        _ => null
    };

    private void AddResourceMarker(DisposeData dispose, Sprite icon)
    {
        var marker = new GameObject($"ResourceMarker_{dispose.prefabName}",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        marker.gameObject.layer = _navigation.Viewport.gameObject.layer;
        marker.SetParent(_navigation.Viewport, false);
        marker.anchorMin = marker.anchorMax = Vector2.one * 0.5f;
        marker.sizeDelta = Vector2.one * (_isExpanded ? _expandedResourceIconSize : _compactResourceIconSize);
        Image image = marker.GetComponent<Image>();
        image.sprite = icon;
        image.raycastTarget = false;
        if (_playerMarker != null) marker.SetSiblingIndex(_playerMarker.GetSiblingIndex());
        _resourceMarkers.Add(dispose, marker);
    }

    private void UpdateResourceMarkerPositions()
    {
        if (_mapData?.Sprite == null || _navigation == null) return;
        foreach (var pair in _resourceMarkers)
        {
            DisposeData dispose = pair.Key;
            if (pair.Value == null) continue;
            Vector3 worldPosition = new Vector3(dispose.tilePosition.x + dispose.localOffset.x,
                0f, dispose.tilePosition.y + dispose.localOffset.y);
            pair.Value.anchoredPosition = _navigation.NormalizedToViewportPosition(
                _mapData.NormalizeWorldPosition(worldPosition));
        }
    }

    private void RemoveResourceMarker(DisposeData dispose)
    {
        if (!_resourceMarkers.Remove(dispose, out RectTransform marker) || marker == null) return;
        marker.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(marker.gameObject);
        else DestroyImmediate(marker.gameObject);
    }

    private void ClearResourceMarkers()
    {
        _removedResources.Clear();
        _removedResources.AddRange(_resourceMarkers.Keys);
        foreach (DisposeData dispose in _removedResources) RemoveResourceMarker(dispose);
        _removedResources.Clear();
        _visibleResources.Clear();
        _orderedResources.Clear();
        _nextResourceRefreshTime = 0f;
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

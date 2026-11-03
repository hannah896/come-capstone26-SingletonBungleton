using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.U2D;

/// <summary>
/// <see cref="WorldMap"/>의 전체 지도를 HUD와 확대 화면에서 공유하는 View다.
/// 표시 크기를 바꿔도 같은 지도, 탐색 상태와 플레이어 마커를 유지한다.
/// </summary>
public class UI_Panel_WorldMap : UI_Panel
{
    [Header("UI References")]
    [SerializeField] private UI_Image _mapImage;
    [SerializeField] private RectTransform _playerMarker;
    [Tooltip("플레이어 이미지와 분리하여 카메라 방향을 따라 회전할 시야각 루트다.")]
    [SerializeField] private RectTransform _sightArea;
    [Tooltip("로컬 인디케이터의 Marker 이미지만 연결한다. 원격 플레이어에게는 시야선을 복제하지 않는다.")]
    [SerializeField] private RectTransform _remoteMarkerTemplate;

    [Header("Marker Settings")]
    [Tooltip("플레이어 이미지는 고정하고 Main Camera의 수평 시야 방향에 맞춰 시야각만 회전한다.")]
    [FormerlySerializedAs("_rotatePlayerMarker")]
    [SerializeField] private bool _rotateSightArea = true;
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
    [SerializeField, Min(0)] private int _maxPooledResourceGraphics = 32;

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
    private bool _isExpanded;
    private readonly Dictionary<Transform, RectTransform> _remoteMarkers = new();
    private readonly HashSet<Transform> _remoteTargets = new();
    private readonly List<Transform> _removedTargets = new();
    private readonly Dictionary<DisposeData, ResourceMarkerData> _resourceMarkers = new();
    private readonly HashSet<DisposeData> _visibleResources = new();
    private readonly List<DisposeData> _removedResources = new();
    private readonly SortedDictionary<int, ResourceColumn> _resourceColumns = new();
    private readonly HashSet<int> _dirtyResourceColumns = new();
    private readonly Stack<UI_ResourceMarkerGraphic> _resourceGraphicPool = new();
    private readonly Dictionary<ChunkData, ResourceChunkCache> _resourceChunkCache = new();
    private readonly List<ResourceMarkerData> _visibleResourceData = new();
    private RectTransform _resourceMarkerLayer;
    private readonly HashSet<ChunkData> _watchedResourceChunks = new();
    private readonly HashSet<ChunkData> _dirtyResourceChunks = new();
    private bool _resourceViewDirty;
    private bool _hasResourceCoverage;
    private Rect _resourceCoverage;
    private float _resourceCoverageZoom;
    private Vector2 _resourceViewportSize;
    private bool _resourceGraphicOrderDirty;
    private bool _resourceAtlasDirty;
    private static readonly IComparer<ResourceMarkerData> ResourceOrder =
        Comparer<ResourceMarkerData>.Create((left, right) => CompareResourcePositions(left.Dispose, right.Dispose));

    // 고정 배치는 캐시하고, 파괴/재생성과 스폰 배제는 청크의 변경 알림으로만 반영한다.
    private sealed class ResourceMarkerData
    {
        public DisposeData Dispose;
        public Vector3 Position;
        public Vector2 Normalized;
        public Sprite Icon;
        public int Column;
    }

    private sealed class ResourceChunkCache
    {
        public List<DisposeData> Source;
        public int Count;
        public readonly List<ResourceMarkerData> Entries = new();
    }

    // X 구간 내부는 모든 Z 청크를 함께 정렬하여 기존 아이콘 겹침 순서를 그대로 유지한다.
    private sealed class ResourceColumn
    {
        public readonly List<ResourceMarkerData> Entries = new();
        public readonly List<UI_ResourceMarkerGraphic.Icon> Icons = new();
        public readonly List<UI_ResourceMarkerGraphic> Graphics = new();
    }

    public Transform Target => _target;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 지도 변경에 따른 배치 재계산을 시계 등 다른 HUD 요소와 분리한다.
        ConfigureMapCanvas();
        if (!TryGetComponent<GraphicRaycaster>(out _)) gameObject.AddComponent<GraphicRaycaster>();

        if (_sightArea == null && _playerMarker != null)
            _sightArea = _playerMarker.Find("SightArea") as RectTransform;
        if (_playerMarker != null) _playerMarker.localRotation = Quaternion.identity;
        if (_remoteMarkerTemplate == null && _playerMarker != null)
            _remoteMarkerTemplate = _playerMarker.Find("Marker") as RectTransform;

        // 배경 이미지와 지도 이미지를 혼동하지 않도록, 전체 지도용 UI_Image는 프리팹에서 명시적으로 연결한다.
        _navigation = UI_MapViewport.Create(_mapImage, null, _playerMarker,
            _zoom, _maxZoom, _zoomStep, fillViewport: false, followTarget: true);
        if (_navigation != null) _mapImage = _navigation.MapImage;
        CreateResourceMarkerLayer();
        if (_navigation != null) _navigation.OnViewChanged += HandleMapViewChanged;
        SpriteAtlasManager.atlasRegistered += HandleResourceAtlasRegistered;
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
        foreach (ResourceColumn column in _resourceColumns.Values)
            foreach (UI_ResourceMarkerGraphic graphic in column.Graphics) graphic.SetIconSize(resourceSize);
        _navigation?.CancelDrag();
        _navigation?.Refresh();
        RefreshResourceMarkers();
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
        TryFindLocalPlayer();
        _nextRemoteSearchTime = 0f;
        // Cinemachine이 카메라 회전을 마친 뒤, UI를 그리기 직전에 방향을 반영한다.
        Canvas.willRenderCanvases += HandleWillRenderCanvases;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        Unbind();
    }

    private void OnDestroy()
    {
        if (_navigation != null) _navigation.OnViewChanged -= HandleMapViewChanged;
        SpriteAtlasManager.atlasRegistered -= HandleResourceAtlasRegistered;
        ClearResourceChunkCache();
    }

    private void HandleWillRenderCanvases()
    {
        UpdateSightAreaRotation();
        // 레이아웃이 LateUpdate 뒤에 바뀐 경우에도 렌더 전에 한 번만 반영한다.
        FlushResourceChanges();
    }

    private void OnTransformParentChanged()
    {
        if (IsInitialized) ConfigureMapCanvas();
    }

    private void ConfigureMapCanvas()
    {
        if (!TryGetComponent(out Canvas mapCanvas)) mapCanvas = gameObject.AddComponent<Canvas>();
        Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        // 중첩 Canvas의 Raycaster도 HUD와 같은 UI 카메라를 사용해야 한다.
        if (parentCanvas != null) mapCanvas.worldCamera = parentCanvas.rootCanvas.worldCamera;
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
        FlushResourceChanges();
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
        if (mapChanged)
        {
            ClearResourceMarkers();
            ClearResourceChunkCache();
        }
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
        ClearResourceChunkCache();
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

    private void HandleMapViewChanged()
    {
        if (_mapData?.LogicData == null || _navigation == null) return;
        Rect visible = _navigation.VisibleNormalizedRect;
        if (!_hasResourceCoverage || _resourceCoverageZoom != _navigation.Zoom
            || _resourceViewportSize != _navigation.Viewport.rect.size
            || visible.xMin < _resourceCoverage.xMin || visible.xMax > _resourceCoverage.xMax
            || visible.yMin < _resourceCoverage.yMin || visible.yMax > _resourceCoverage.yMax)
            _resourceViewDirty = true;
    }

    private void HandleResourceStateChanged(ChunkData chunk)
    {
        // 화면 밖 청크의 변경은 나중에 진입할 때 읽는다. 같은 청크의 연속 변경은 한 번으로 합친다.
        if (_watchedResourceChunks.Contains(chunk)) _dirtyResourceChunks.Add(chunk);
    }

    private void HandleResourceAtlasRegistered(SpriteAtlas atlas)
    {
        if ((_branchIcon != null && atlas.CanBindTo(_branchIcon))
            || (_bushIcon != null && atlas.CanBindTo(_bushIcon))
            || (_firIcon != null && atlas.CanBindTo(_firIcon))
            || (_grassIcon != null && atlas.CanBindTo(_grassIcon))
            || (_rockIcon != null && atlas.CanBindTo(_rockIcon)))
            _resourceAtlasDirty = true;
    }

    private void FlushResourceChanges()
    {
        if (_resourceAtlasDirty)
        {
            foreach (int column in _resourceColumns.Keys) _dirtyResourceColumns.Add(column);
            _resourceAtlasDirty = false;
        }
        if (_resourceViewDirty)
        {
            RefreshResourceMarkers();
            return;
        }
        if (_mapData?.LogicData == null || (_dirtyResourceChunks.Count == 0 && _dirtyResourceColumns.Count == 0)) return;
        WorldObjectSpawner spawner = GetResourceSpawner();
        foreach (ChunkData chunk in _dirtyResourceChunks)
        {
            foreach (ResourceMarkerData entry in GetResourceEntries(chunk))
            {
                if (ShouldShowResource(entry, chunk, spawner))
                {
                    if (!_resourceMarkers.ContainsKey(entry.Dispose)) AddResourceMarker(entry);
                }
                else RemoveResourceMarker(entry.Dispose);
            }
        }
        _dirtyResourceChunks.Clear();
        RebuildResourceGraphics();
    }

    private WorldObjectSpawner GetResourceSpawner()
    {
        WorldGenManager world = WorldGenManager.Instance;
        return world != null && world.CurrentLogicData == _mapData.LogicData
            ? world.ChunkDirector?.ObjectSpawner : null;
    }

    private bool ShouldShowResource(ResourceMarkerData entry, ChunkData chunk, WorldObjectSpawner spawner)
    {
        return entry.Normalized.x >= _resourceCoverage.xMin && entry.Normalized.x <= _resourceCoverage.xMax
            && entry.Normalized.y >= _resourceCoverage.yMin && entry.Normalized.y <= _resourceCoverage.yMax
            && !chunk.IsObjectDestroyed(entry.Dispose.instanceId)
            && spawner?.IsSpawnExcluded(entry.Dispose.instanceId) != true
            && !WorldSpawnArea.Contains(_mapData.LogicData, entry.Position);
    }

    private void ClearResourceChunkCache()
    {
        foreach (ChunkData chunk in _resourceChunkCache.Keys)
            chunk.OnResourceStateChanged -= HandleResourceStateChanged;
        _resourceChunkCache.Clear();
        _watchedResourceChunks.Clear();
        _dirtyResourceChunks.Clear();
    }

    private void RefreshResourceMarkers()
    {
        if (_mapData?.Sprite == null || _mapData.LogicData == null || _navigation == null
            || _resourceMarkerLayer == null) return;

        _navigation.Refresh();
        Rect visible = _navigation.VisibleNormalizedRect;
        // 각 변에 10% 여유를 두어 작은 이동에는 목록을 탐색하지 않고 부모 이동/마스킹만 수행한다.
        _resourceCoverage = UnityEngine.Rect.MinMaxRect(
            Mathf.Max(0f, visible.xMin - visible.width * 0.1f),
            Mathf.Max(0f, visible.yMin - visible.height * 0.1f),
            Mathf.Min(1f, visible.xMax + visible.width * 0.1f),
            Mathf.Min(1f, visible.yMax + visible.height * 0.1f));
        _resourceCoverageZoom = _navigation.Zoom;
        _resourceViewportSize = _navigation.Viewport.rect.size;
        _hasResourceCoverage = true;
        _resourceViewDirty = false;
        _dirtyResourceChunks.Clear();
        _watchedResourceChunks.Clear();
        float minX = _resourceCoverage.xMin * _mapData.TerrainSize.x;
        float maxX = _resourceCoverage.xMax * _mapData.TerrainSize.x;
        float minZ = _resourceCoverage.yMin * _mapData.TerrainSize.y;
        float maxZ = _resourceCoverage.yMax * _mapData.TerrainSize.y;
        int chunkSize = _mapData.LogicData.ChunkSize;
        if (chunkSize <= 0) return;
        // 청크 경계의 localOffset과 경계 위 자원을 포함하도록 인접 청크까지 조회한다.
        int minChunkX = Mathf.Max(0, Mathf.FloorToInt(minX / chunkSize) - 1);
        int maxChunkX = Mathf.Min(_mapData.TerrainSize.x / chunkSize, Mathf.FloorToInt(maxX / chunkSize) + 1);
        int minChunkZ = Mathf.Max(0, Mathf.FloorToInt(minZ / chunkSize) - 1);
        int maxChunkZ = Mathf.Min(_mapData.TerrainSize.y / chunkSize, Mathf.FloorToInt(maxZ / chunkSize) + 1);
        WorldObjectSpawner spawner = GetResourceSpawner();
        _visibleResources.Clear();
        _visibleResourceData.Clear();
        // 전체 청크 목록을 복사/순회하지 않고 표시 영역의 좌표만 읽는다.
        for (int z = minChunkZ; z <= maxChunkZ; z++)
        for (int x = minChunkX; x <= maxChunkX; x++)
        {
            if (!_mapData.LogicData.TryGetChunk(new Vector2Int(x, z), out ChunkData chunk)
                || chunk?.DisposeDatas == null) continue;
            _watchedResourceChunks.Add(chunk);
            foreach (ResourceMarkerData entry in GetResourceEntries(chunk))
            {
                DisposeData dispose = entry.Dispose;
                if (!ShouldShowResource(entry, chunk, spawner)) continue;
                _visibleResources.Add(dispose);
                _visibleResourceData.Add(entry);
            }
        }

        _removedResources.Clear();
        foreach (DisposeData dispose in _resourceMarkers.Keys)
            if (!_visibleResources.Contains(dispose)) _removedResources.Add(dispose);
        foreach (DisposeData dispose in _removedResources) RemoveResourceMarker(dispose);
        _removedResources.Clear();
        // 이탈한 마커를 먼저 반납하여 같은 갱신에서 새로 진입한 자원에 재사용한다.
        foreach (ResourceMarkerData entry in _visibleResourceData)
            if (!_resourceMarkers.ContainsKey(entry.Dispose)) AddResourceMarker(entry);
        RebuildResourceGraphics();
    }

    private List<ResourceMarkerData> GetResourceEntries(ChunkData chunk)
    {
        if (!_resourceChunkCache.TryGetValue(chunk, out ResourceChunkCache cache))
        {
            cache = new ResourceChunkCache();
            _resourceChunkCache.Add(chunk, cache);
            chunk.OnResourceStateChanged += HandleResourceStateChanged;
        }
        if (ReferenceEquals(cache.Source, chunk.DisposeDatas) && cache.Count == chunk.DisposeDatas.Count)
            return cache.Entries;

        cache.Source = chunk.DisposeDatas;
        cache.Count = cache.Source.Count;
        cache.Entries.Clear();
        foreach (DisposeData dispose in cache.Source)
        {
            if (dispose == null) continue;
            Sprite icon = GetResourceIcon(dispose.prefabName);
            if (icon == null) continue;
            Vector3 position = new Vector3(dispose.tilePosition.x + dispose.localOffset.x,
                0f, dispose.tilePosition.y + dispose.localOffset.y);
            if (position.x < 0f || position.z < 0f || position.x > _mapData.TerrainSize.x
                || position.z > _mapData.TerrainSize.y) continue;
            cache.Entries.Add(new ResourceMarkerData
            {
                Dispose = dispose, Position = position, Icon = icon,
                Normalized = _mapData.NormalizeWorldPosition(position),
                Column = Mathf.FloorToInt(position.x / Mathf.Max(1, _mapData.LogicData.ChunkSize))
            });
        }
        return cache.Entries;
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

    private void CreateResourceMarkerLayer()
    {
        if (_mapImage == null || _resourceMarkerLayer != null) return;
        _resourceMarkerLayer = new GameObject("ResourceMarkers", typeof(RectTransform)).GetComponent<RectTransform>();
        _resourceMarkerLayer.gameObject.layer = _mapImage.gameObject.layer;
        // 지도와 함께 이동하되 scale은 1로 유지하여 줌에도 아이콘의 픽셀 크기를 고정한다.
        _resourceMarkerLayer.SetParent(_mapImage.Rect, false);
        _resourceMarkerLayer.anchorMin = Vector2.zero;
        _resourceMarkerLayer.anchorMax = Vector2.one;
        _resourceMarkerLayer.sizeDelta = Vector2.zero;
        _resourceMarkerLayer.anchoredPosition = Vector2.zero;
        // 자원 메시의 배치 계산을 매 프레임 움직이는 플레이어/시야각과 분리한다.
        _resourceMarkerLayer.gameObject.AddComponent<Canvas>();
    }

    private void AddResourceMarker(ResourceMarkerData entry)
    {
        _resourceMarkers.Add(entry.Dispose, entry);
        if (!_resourceColumns.TryGetValue(entry.Column, out ResourceColumn column))
        {
            column = new ResourceColumn();
            _resourceColumns.Add(entry.Column, column);
            _resourceGraphicOrderDirty = true;
        }
        column.Entries.Add(entry);
        _dirtyResourceColumns.Add(entry.Column);
    }

    private void RemoveResourceMarker(DisposeData dispose)
    {
        if (!_resourceMarkers.Remove(dispose, out ResourceMarkerData entry)) return;
        _resourceColumns[entry.Column].Entries.Remove(entry);
        _dirtyResourceColumns.Add(entry.Column);
    }

    private UI_ResourceMarkerGraphic RentResourceGraphic()
    {
        UI_ResourceMarkerGraphic graphic = _resourceGraphicPool.Count > 0 ? _resourceGraphicPool.Pop() :
            new GameObject("ResourceMarkerBatch", typeof(RectTransform), typeof(UI_ResourceMarkerGraphic))
                .GetComponent<UI_ResourceMarkerGraphic>();
        graphic.gameObject.layer = _resourceMarkerLayer.gameObject.layer;
        RectTransform rect = graphic.rectTransform;
        if (rect.parent != _resourceMarkerLayer) rect.SetParent(_resourceMarkerLayer, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        graphic.gameObject.SetActive(true);
        return graphic;
    }

    private void ReturnResourceGraphic(UI_ResourceMarkerGraphic graphic)
    {
        graphic.gameObject.SetActive(false);
        graphic.ClearIcons();
        graphic.transform.SetAsLastSibling();
        if (_resourceGraphicPool.Count < _maxPooledResourceGraphics) _resourceGraphicPool.Push(graphic);
        else if (Application.isPlaying) Destroy(graphic.gameObject);
        else DestroyImmediate(graphic.gameObject);
    }

    private void RebuildResourceGraphics()
    {
        foreach (int key in _dirtyResourceColumns)
        {
            ResourceColumn column = _resourceColumns[key];
            column.Entries.Sort(ResourceOrder);
            column.Icons.Clear();
            foreach (ResourceMarkerData entry in column.Entries)
                if (entry.Icon != null && entry.Icon.texture != null)
                    column.Icons.Add(new UI_ResourceMarkerGraphic.Icon(entry.Icon, entry.Normalized));

            int used = 0;
            for (int start = 0; start < column.Icons.Count;)
            {
                Texture texture = column.Icons[start].Sprite.texture;
                int end = start + 1;
                // Atlas 준비 전에도 서로 다른 텍스처를 한 메시로 잘못 그리지 않는다.
                while (end < column.Icons.Count && end - start < UI_ResourceMarkerGraphic.MaxIcons
                    && column.Icons[end].Sprite.texture == texture) end++;
                if (used == column.Graphics.Count)
                {
                    column.Graphics.Add(RentResourceGraphic());
                    _resourceGraphicOrderDirty = true;
                }
                column.Graphics[used++].SetIcons(column.Icons, start, end - start, texture,
                    _isExpanded ? _expandedResourceIconSize : _compactResourceIconSize);
                start = end;
            }
            for (int i = column.Graphics.Count - 1; i >= used; i--)
            {
                ReturnResourceGraphic(column.Graphics[i]);
                column.Graphics.RemoveAt(i);
                _resourceGraphicOrderDirty = true;
            }
            if (column.Entries.Count == 0) _resourceColumns.Remove(key);
        }
        _dirtyResourceColumns.Clear();
        if (!_resourceGraphicOrderDirty) return;
        int sibling = 0;
        foreach (ResourceColumn column in _resourceColumns.Values)
            foreach (UI_ResourceMarkerGraphic graphic in column.Graphics)
                if (graphic.transform.GetSiblingIndex() != sibling++) graphic.transform.SetSiblingIndex(sibling - 1);
        _resourceGraphicOrderDirty = false;
    }

    private void ClearResourceMarkers()
    {
        foreach (ResourceColumn column in _resourceColumns.Values)
            foreach (UI_ResourceMarkerGraphic graphic in column.Graphics) ReturnResourceGraphic(graphic);
        _resourceColumns.Clear();
        _dirtyResourceColumns.Clear();
        _resourceGraphicOrderDirty = false;
        _resourceAtlasDirty = false;
        _resourceMarkers.Clear();
        _removedResources.Clear();
        _visibleResources.Clear();
        _visibleResourceData.Clear();
        _hasResourceCoverage = false;
        _resourceViewDirty = false;
        _watchedResourceChunks.Clear();
        _dirtyResourceChunks.Clear();
    }

    private void UpdateSightAreaRotation()
    {
        if (!_rotateSightArea || !isActiveAndEnabled || _target == null
            || _sightArea == null || !_sightArea.gameObject.activeInHierarchy) return;

        Camera viewCamera = Camera.main;
        Vector3 forward = viewCamera != null ? viewCamera.transform.forward : _target.forward;
        // 지도 위쪽은 월드 +Z다. 상하 시선은 제외하고 수직을 바라볼 때는 마지막 방향을 유지한다.
        Vector2 heading = new Vector2(forward.x, forward.z);
        if (heading.sqrMagnitude < 0.000001f) return;

        float yaw = Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
        _sightArea.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 월드 생성과 관련된 모든 디렉터(그래프, 청크, 렌더링)를 총괄하는 매니저 클래스.
/// 생성 이후 월드 관련 런타임 시스템(시뮬레이션, 날씨 등)도 이쪽에서 관리하고 있습니다.(필요한 경우 분리 예정)
/// </summary>
public class WorldGenManager : MonoBehaviour
{
    #region 임시 싱글톤
    //TODO: Main에 옮기던가 역할 분리 등 필요
    //임시 싱글톤 패턴 
    public static WorldGenManager Instance { get; private set; }
    void Awake()
    {
        // 초기화
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    #endregion

    
    [SerializeField] private WorldGraphDirector _worldGraphDirector;
    [SerializeField] private WorldChunkDirector _worldChunkDirector;
    [SerializeField] private WorldRenderDirector _worldRenderDirector;
    [SerializeField] private DynamicSpawnDirector _dynamicSpawnDirector;
    [SerializeField] private WorldSettings _worldSettings;

    [SerializeField] private WorldSimulationManager _simulationManager;
    [SerializeField] private WorldMap _worldMap;

    private const string PLAYER_ADDRESSKEY = "Player";
    private const string GROUND_LAYER_NAME = "Ground";
    private const string INVISIBLE_WALL_ADDRESSKEY = "InvisibleWall";
    private const float COAST_WALL_THICKNESS = 2.0f;            //벽 두께
    private GameObject _playerInstance;
    private GameObject _coastBoundaryRoot;
    private readonly List<GameObject> _coastWalls = new();

    [SerializeField] private int _currentSeed = 0;            // 현재 유지 중인 시드값

    private string _worldSettingLabel = "TestWorldSettings";

    private CancellationTokenSource _cts;
    private bool _isWorldSettingsLoaded;
    private bool _isStartedWorldGeneration;

    public WorldGraphDirector GraphDirector => _worldGraphDirector;
    public WorldSettings WorldSettings => _worldSettings;

    /// <summary>WorldSettings 에셋 로드가 완료되어 월드 생성이 가능한 상태인지 여부.</summary>
    public bool IsWorldSettingsLoaded => _isWorldSettingsLoaded;

    /// <summary>
    /// 플레이어 최초 스폰에 사용한 StartRegion 좌표. 부활 지점으로도 사용된다.
    /// 낙하 여유를 두고 지면보다 높게 잡혀 있으므로, 그대로 쓰면 공중에서 떨어진다.
    /// </summary>
    public Vector3 PlayerSpawnPosition { get; private set; }

    /// <summary>스폰 좌표가 확정되었는지 여부 (월드 생성 완료 전에는 false).</summary>
    public bool HasPlayerSpawnPosition { get; private set; }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        ClearCoastBoundaries();
    }

    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        _cts = new CancellationTokenSource();

        // 월드 관련 디렉터의 컴포넌트 유무 확인 및 부착
        if (_worldGraphDirector == null)
            _worldGraphDirector = Extensions.GetOrAddComponent<WorldGraphDirector>(this.gameObject);
        if (_worldRenderDirector == null)
            _worldRenderDirector = Extensions.GetOrAddComponent<WorldRenderDirector>(this.gameObject);
        if (_worldChunkDirector == null)
            _worldChunkDirector = Extensions.GetOrAddComponent<WorldChunkDirector>(this.gameObject);
        if (_dynamicSpawnDirector == null)
            _dynamicSpawnDirector = Extensions.GetOrAddComponent<DynamicSpawnDirector>(this.gameObject);
        if (_worldMap == null)
            _worldMap = WorldMap.Instance ?? Extensions.GetOrAddComponent<WorldMap>(this.gameObject);

        await LoadWorldSettingsAsync(_cts.Token);
        
    }

    #region Load StoryWorldSettings
    private async UniTask LoadWorldSettingsAsync(CancellationToken ct)
    {
        try
        {
            _worldSettings = await Extensions.LoadAssetAsync<WorldSettings>(
                _worldSettingLabel,
                AssetCacheType.Required,
                ct
            );
            _isWorldSettingsLoaded = (_worldSettings != null);
            Debug.Log("WorldSettings 로드 완료");
        }
        catch (System.OperationCanceledException)
        {
            _isWorldSettingsLoaded = false;
            Debug.Log("WorldSettings 로드 취소됨");
        }
        catch (System.Exception e)
        {
            _isWorldSettingsLoaded = false;
            Debug.LogError($"WorldSettings 로드 실패: {e.Message}");
        }
    }
    #endregion

    /// <summary>
    /// 시드를 내부에서 랜덤 생성하여 월드를 생성합니다. (기존 호환용)
    /// </summary>
    public UniTask GenerateWorldFromUI(WorldBranchSetting branch, WorldLoopSetting loop)
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        return GenerateWorld(branch, loop, seed, WorldSize.Large);
    }

    /// <summary>
    /// 외부에서 확정한 설정(시드 포함)으로 월드를 생성합니다.
    /// 로비에서 결정한 WorldGenRequest를 게임씬이 그대로 주입할 때 사용합니다.
    /// </summary>
    /// <param name="external">씬 전환 등 외부 취소 토큰. 전달 시 내부 토큰과 연결됩니다.</param>
    public UniTask GenerateWorld(
        WorldBranchSetting branch,
        WorldLoopSetting loop,
        int seed,
        WorldSize size = WorldSize.Large,
        CancellationToken external = default)
    {
        return GenerateWorldWithSettings(() =>
        {
            _currentSeed = seed;

            _worldSettings.WorldSeed = seed;
            _worldSettings.WorldSize = size;
            _worldSettings.WorldBranch = branch;
            _worldSettings.WorldLoop = loop;

            Debug.Log($"월드 생성. 시드: {seed}, Size: {size}, Branch: {branch}, Loop: {loop}");
        }, external);
    }

    /// <summary>월드 생성 진행률(0~1)과 현재 단계 설명.</summary>
    public event Action<float, string> OnProgress;

    private void ReportProgress(float value, string label)
    {
        OnProgress?.Invoke(Mathf.Clamp01(value), label);
    }

    private async UniTask GenerateWorldWithSettings(Action applySettings, CancellationToken external = default)
    {
        _isStartedWorldGeneration = true;
        _dynamicSpawnDirector?.Shutdown();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = external.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(external)
            : new CancellationTokenSource();

        ClearCoastBoundaries();

        if (!_isWorldSettingsLoaded || _worldSettings == null)
        {
            Debug.LogWarning("WorldSettings가 아직 로드되지 않았습니다.");
            return;
        }

        try
        {
            Debug.Log("=== 월드 생성 시작 ===");
            ReportProgress(0.05f, "월드 설정 적용");

            applySettings?.Invoke();

            ReportProgress(0.15f, "그래프 생성");

            // ==========================================================
            // 1단계: 논리 데이터 생성 및 청크 분할
            // ==========================================================
            await _worldGraphDirector.GenerateWorldLogicWithSettings(_worldSettings, _cts.Token);
            Debug.Log($"월드 그래프 생성 완료. 시드: {_worldSettings.WorldSeed}");

            var graphData = _worldGraphDirector.GetWorldGraphData();
            var logicData = _worldGraphDirector.GetWorldLogicData();

            ReportProgress(0.3f, "해안 경계 생성");
            await BuildCoastBoundariesAsync(logicData, _cts.Token);

            if (_worldMap == null)
                _worldMap = WorldMap.Instance ?? Extensions.GetOrAddComponent<WorldMap>(this.gameObject);
            await _worldMap.BuildAsync(logicData, graphData, _worldSettings, ct: _cts.Token);

            ReportProgress(0.35f, "렌더 초기화");

            // ==========================================================
            // 2단계: 렌더 디렉터 초기화 및 에셋 로드 
            // ==========================================================
            _worldRenderDirector.ClearAllChunks();
            await _worldRenderDirector.InitializeAsync(_worldSettings, graphData, _cts.Token);

            ReportProgress(0.55f, "스폰 좌표");

            // ==========================================================
            // 3단계: 플레이어 스폰 좌표 가져오기
            // ==========================================================
            Vector2Int mapSize = _worldSettings.GetWorldSize();
            int startingX = Mathf.RoundToInt(mapSize.x * 0.5f);
            int startingZ = Mathf.RoundToInt(mapSize.y * 0.5f);

            Vector2Int spawnTile = logicData.SpawnTile;
            if (spawnTile.x >= 0 && spawnTile.y >= 0)
            {
                startingX = spawnTile.x;
                startingZ = spawnTile.y;
                Debug.Log($"✅ StartRegion 스폰 타일 좌표: ({startingX}, {startingZ})");
            }
            else
            {
                Debug.LogWarning("🚨 StartRegion 스폰 좌표를 찾지 못해 맵 중앙 좌표를 사용합니다.");
            }


            Vector2Int spawnChunkCoord = logicData.GetChunkCoord(startingX, startingZ);

            ReportProgress(0.7f, "스폰 지역 렌더링 중");

            // ==========================================================
            // 4단계: 청크 디렉터 초기화 및 스폰 지역 확정 렌더링 대기
            // ==========================================================
            _worldChunkDirector.Initialize(logicData, _worldRenderDirector);

            await _worldChunkDirector.LoadInitialSpawnAreaAsync(spawnChunkCoord);
            Debug.Log("월드 생성이 완료되었습니다!");

            ReportProgress(0.85f, "플레이어 준비");

            // ==========================================================
            // 4.5단계: 스폰 대기
            //   맵이 완전히 생성된 뒤 일정 시간을 두고 캐릭터를 소환한다.
            //   지형·콜라이더가 자리를 잡기 전에 캐릭터가 놓이면 허공에서 떨어지기 때문.
            //   멀티에서는 각 피어가 자기 월드 생성 후 이 대기를 거친 뒤에야
            //   NotifyWorldReady로 준비 완료를 알리므로, 호스트가 스폰하는
            //   "상대방 캐릭터"에도 동일하게 적용된다.
            // ==========================================================
            await WaitBeforeSpawnAsync(_cts.Token);

            // ==========================================================
            // 5단계: 플레이어 스폰
            //   - 멀티: 호스트가 Runner.Spawn으로 네트워크 스폰(각 클라는 복제된 로컬 플레이어를 청크 타깃으로)
            //   - 싱글: 기존 로컬 스폰
            // ==========================================================
            float spawnTileHeight = logicData.GetHeightAt(startingX, startingZ);
            Vector3 spawnPos = new Vector3(startingX, spawnTileHeight + 20f, startingZ);

            // 부활 지점으로 재사용한다 (Player.Revive)
            PlayerSpawnPosition = spawnPos;
            HasPlayerSpawnPosition = true;

            bool isMultiplayer = Main.Network != null && Main.Network.IsInRoom;
            if (isMultiplayer)
            {
                // 캐릭터는 호스트가 네트워크로 스폰한다. 클라는 로컬 스폰하지 않는다.
                Main.Network.NotifyWorldReady(spawnPos);

                // 자신의 로컬 플레이어가 복제되어 등장하면 청크 스트리밍 타깃으로 지정
                Player localPlayer = await WaitForLocalNetworkPlayerAsync(_cts.Token);
                if (localPlayer != null)
                {
                    _playerInstance = localPlayer.gameObject;
                    _worldChunkDirector.isPlayerSpawned = true;
                    _worldChunkDirector.SetTarget(_playerInstance.transform);
                }
                else
                {
                    Debug.LogWarning("🚨 로컬 네트워크 플레이어를 찾지 못했습니다.");
                }
            }
            else
            {
                if (_playerInstance == null)
                {
                    Player player = await Extensions.Instantiate<Player>("Player");
                    if (player != null)
                    {
                        _playerInstance = player.gameObject;
                    }
                }

                if (_playerInstance != null)
                {
                    _playerInstance.SetActive(true);

                    // CharacterController는 자체 내부 좌표를 갖고 있어 transform만 옮기면
                    // 다음 Move에서 되돌려진다(스폰 직후 원점으로 튕기는 원인).
                    // 반드시 PlayerMotor.Teleport(비활성화 → 이동 → 재활성화)를 거쳐야 한다.
                    if (_playerInstance.TryGetComponent(out PlayerMotor playerMotor))
                        playerMotor.Teleport(spawnPos);
                    else
                        _playerInstance.transform.position = spawnPos;

                    _worldChunkDirector.isPlayerSpawned = true;
                    _worldChunkDirector.SetTarget(_playerInstance.transform);
                }
                else
                {
                    Debug.LogWarning("🚨 'Player' 오브젝트가 없습니다!");
                }
            }

            _simulationManager = Extensions.GetOrAddComponent<WorldSimulationManager>(this.gameObject);
            _simulationManager.Initialize(_worldChunkDirector);

            _dynamicSpawnDirector.Initialize(
                logicData,
                graphData,
                _worldChunkDirector,
                _worldSettings.DynamicSpawnSettings,
                _cts.Token);

            ReportProgress(1f, "완료");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("맵 생성 취소됨");
        }
    }

    /// <summary>
    /// 논리 월드의 육지-바다 경계에 InvisibleWall을 생성합니다.
    /// 같은 방향으로 이어진 타일 경계는 하나의 벽으로 병합합니다.
    /// </summary>
    private async UniTask BuildCoastBoundariesAsync(WorldLogicData logicData, CancellationToken ct)
    {
        if (logicData?.TerritoryWorld == null) return;

        _coastBoundaryRoot = new GameObject("@WorldCoastBoundary");
        _coastBoundaryRoot.transform.SetParent(transform, false);

        int width = logicData.TerrainSize.x;
        int height = logicData.TerrainSize.y;
        float seaLevel = _worldSettings.GetHeight(HeightLevel.Ocean);

        // 남쪽/북쪽 해안: X 방향으로 이어진 타일 경계를 하나의 벽으로 병합
        for (int z = 0; z < height; z++)
        {
            await SpawnHorizontalCoastSegmentsAsync(logicData, z, -1, z, seaLevel, ct);
            await SpawnHorizontalCoastSegmentsAsync(logicData, z, 1, z + 1, seaLevel, ct);
        }

        // 서쪽/동쪽 해안: Z 방향으로 이어진 타일 경계를 하나의 벽으로 병합
        for (int x = 0; x < width; x++)
        {
            await SpawnVerticalCoastSegmentsAsync(logicData, x, -1, x, seaLevel, ct);
            await SpawnVerticalCoastSegmentsAsync(logicData, x, 1, x + 1, seaLevel, ct);
        }

        Debug.Log($"[WorldGenManager] 해안 투명벽 {_coastWalls.Count}개 생성 완료");
    }

    private async UniTask SpawnHorizontalCoastSegmentsAsync(
        WorldLogicData logicData,
        int z,
        int oceanDirectionZ,
        float wallZ,
        float seaLevel,
        CancellationToken ct)
    {
        int width = logicData.TerrainSize.x;

        for (int x = 0; x < width;)
        {
            if (!IsCoastEdge(logicData, x, z, 0, oceanDirectionZ))
            {
                x++;
                continue;
            }

            int startX = x;
            while (x < width && IsCoastEdge(logicData, x, z, 0, oceanDirectionZ))
                x++;

            float segmentLength = x - startX;
            Vector3 position = new Vector3(startX + segmentLength * 0.5f, seaLevel, wallZ);
            Vector3 scale = new Vector3(segmentLength, 1f, COAST_WALL_THICKNESS);
            await SpawnCoastWallAsync(position, scale, ct);
        }
    }

    private async UniTask SpawnVerticalCoastSegmentsAsync(
        WorldLogicData logicData,
        int x,
        int oceanDirectionX,
        float wallX,
        float seaLevel,
        CancellationToken ct)
    {
        int height = logicData.TerrainSize.y;

        for (int z = 0; z < height;)
        {
            if (!IsCoastEdge(logicData, x, z, oceanDirectionX, 0))
            {
                z++;
                continue;
            }

            int startZ = z;
            while (z < height && IsCoastEdge(logicData, x, z, oceanDirectionX, 0))
                z++;

            float segmentLength = z - startZ;
            Vector3 position = new Vector3(wallX, seaLevel, startZ + segmentLength * 0.5f);
            Vector3 scale = new Vector3(COAST_WALL_THICKNESS, 1f, segmentLength);
            await SpawnCoastWallAsync(position, scale, ct);
        }
    }

    private static bool IsCoastEdge(WorldLogicData logicData, int x, int z, int neighborOffsetX, int neighborOffsetZ)
    {
        if (logicData.TerritoryWorld[x, z] < 0) return false;

        // Influence Map 생성 단계에서 바다와 직접 맞닿은 육지는 거리 1로 기록된다.
        if (logicData.DistanceToOceanWorld != null && logicData.DistanceToOceanWorld[x, z] != 1)
            return false;

        int neighborX = x + neighborOffsetX;
        int neighborZ = z + neighborOffsetZ;
        bool isOutsideWorld = neighborX < 0 || neighborX >= logicData.TerrainSize.x ||
                              neighborZ < 0 || neighborZ >= logicData.TerrainSize.y;

        return isOutsideWorld || logicData.TerritoryWorld[neighborX, neighborZ] < 0;
    }

    private async UniTask SpawnCoastWallAsync(Vector3 position, Vector3 scale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        GameObject wall = await Extensions.SpawnAsync(
            INVISIBLE_WALL_ADDRESSKEY,
            _coastBoundaryRoot.transform,
            ct);

        if (wall == null) return;

        wall.transform.SetPositionAndRotation(position, Quaternion.identity);
        wall.transform.localScale = scale;
        _coastWalls.Add(wall);
    }

    private void ClearCoastBoundaries()
    {
        foreach (GameObject wall in _coastWalls)
        {
            if (wall != null)
                Extensions.Despawn(wall);
        }
        _coastWalls.Clear();

        if (_coastBoundaryRoot != null)
        {
            Destroy(_coastBoundaryRoot);
            _coastBoundaryRoot = null;
        }
    }

    /// <summary>
    /// 맵 생성이 끝난 뒤 캐릭터를 소환하기까지 두는 대기 시간(초).
    /// (WorldGenManager는 동적 생성돼 인스펙터 주입 경로가 없으므로 상수로 둔다)
    /// </summary>
    private const float SpawnDelaySeconds = 5f;

    // 스폰 전 대기. 로딩 게이지가 멈춘 것처럼 보이지 않도록 진행률을 함께 올린다.
    private async UniTask WaitBeforeSpawnAsync(CancellationToken token)
    {
        const float startProgress = 0.85f;
        const float endProgress = 0.95f;

        float elapsed = 0f;
        while (elapsed < SpawnDelaySeconds)
        {
            // 게임 속도(timeScale) 조작에 영향받지 않도록 unscaled 기준으로 센다
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / SpawnDelaySeconds);
            ReportProgress(Mathf.Lerp(startProgress, endProgress, t), "플레이어 준비");
        }
    }

    // 멀티: 자신의 로컬 플레이어 캐릭터가 네트워크로 복제되어 등장할 때까지 대기한다.
    private async UniTask<Player> WaitForLocalNetworkPlayerAsync(CancellationToken token)
    {
        const float timeoutSec = 15f;
        float elapsed = 0f;

        while (elapsed < timeoutSec)
        {
            Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
            foreach (Player p in players)
            {
                if (p != null && p.IsLocalPlayer) return p;
            }

            await UniTask.Delay(100, cancellationToken: token);
            elapsed += 0.1f;
        }

        return null;
    }


#if UNITY_EDITOR
    #region Debug & Gizmos
    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _showConnections = true;
    [SerializeField] private bool _showMapBounds = true;
    [SerializeField] private bool _drawTerritoryGrid = false;
    [SerializeField] private bool _useHeightVisualization = true;
    [SerializeField] private bool _showRegionNames = true; // ★ 텍스트 표시 토글 추가

    public enum InfluenceDebugType { None, CoastlineWeight, RegionEdgeWeight }
    [Header("Heatmap Mode")]
    [Tooltip("그리드(TerritoryGrid) 렌더링 시 어떤 데이터를 시각화할지 선택합니다.")]
    [SerializeField] private InfluenceDebugType _influenceDebugType = InfluenceDebugType.None;

    void OnDrawGizmos()
    {
        if (!_isStartedWorldGeneration || !_drawGizmos || _worldGraphDirector == null || _worldSettings == null) return;

        var graphData = _worldGraphDirector.GetWorldGraphData();
        var logicData = _worldGraphDirector.GetWorldLogicData();

        if (_showMapBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_worldSettings.GetWorldSize().x / 2f, 0, _worldSettings.GetWorldSize().y / 2f);
            Vector3 size = new Vector3(_worldSettings.GetWorldSize().x, 0, _worldSettings.GetWorldSize().y);
            Gizmos.DrawWireCube(center, size);
        }

        // 1. 노드 중심점 그리기
        foreach (var node in graphData.Nodes)
        {
            float worldX = node.Position.x;
            float worldZ = node.Position.y;
            Vector3 nodePos = new Vector3(worldX, 0, worldZ);

            if (_useHeightVisualization && logicData?.HeightWorld != null)
            {
                int tileX = Mathf.RoundToInt(node.Position.x);
                int tileY = Mathf.RoundToInt(node.Position.y);
                float height = logicData.GetHeightAt(tileX, tileY);
                nodePos.y = height;
            }

            Gizmos.color = node.RegionData != null ? node.BiomeData.DebugColor : Color.white;
            Gizmos.DrawSphere(nodePos, 1.0f);

            if (_showRegionNames && node.RegionData != null)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.black;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;

                Vector3 labelPos = nodePos + Vector3.up * 1.5f;
                UnityEditor.Handles.Label(labelPos, $"{node.RegionData.RegionName}: x:{nodePos.x} y:{nodePos.y} z:{nodePos.z}", style);
            }
        }

        // 2. 연결선 그리기
        if (_showConnections && graphData.NodeConnections != null)
        {
            Gizmos.color = Color.white;
            foreach (var conn in graphData.NodeConnections)
            {
                if (conn.ParentNode == null || conn.ChildNode == null) continue;
                var parentNode = conn.ParentNode;
                var childNode = conn.ChildNode;

                float startX = parentNode.Position.x;
                float startZ = parentNode.Position.y;
                float endX = childNode.Position.x;
                float endZ = childNode.Position.y;

                Vector3 start = new Vector3(startX, 0, startZ);
                Vector3 end = new Vector3(endX, 0, endZ);

                // 높이 적용
                if (_useHeightVisualization && logicData?.HeightWorld != null)
                {
                    int startTileX = Mathf.RoundToInt(parentNode.Position.x);
                    int startTileY = Mathf.RoundToInt(parentNode.Position.x);
                    int endTileX = Mathf.RoundToInt(childNode.Position.x);
                    int endTileY = Mathf.RoundToInt(childNode.Position.y);

                    start.y = logicData.GetHeightAt(startTileX, startTileY);
                    end.y = logicData.GetHeightAt(endTileX, endTileY);
                }

                if (parentNode.RegionData != null && childNode.RegionData != null)
                {
                    // 연결된 두 노드의 Room 색상 평균으로 선 색상 설정
                    Color startColor = parentNode.BiomeData.DebugColor;
                    Color endColor = childNode.BiomeData.DebugColor;
                    Gizmos.color = Color.Lerp(startColor, endColor, 0.5f);
                }
                else
                {
                    Gizmos.color = Color.white; // 기본 흰색
                }

                Gizmos.DrawLine(start, end);
            }
        }

        // 3. 영토 그리드(보로노이) 그리기
        if (_drawTerritoryGrid && logicData.TerritoryWorld != null)
        {
            DrawTerritoryGizmos();
        }
    }

    private void DrawTerritoryGizmos()
    {
        var logicData = _worldGraphDirector.GetWorldLogicData();
        var graphData = _worldGraphDirector.GetWorldGraphData();
        int[,] territory = logicData.TerritoryWorld;
        float[,] heightWorld = logicData.HeightWorld;
        int widthX = territory.GetLength(0);
        int widthY = territory.GetLength(1);

        // 성능을 위해 스텝 건너뛰기 (전체 다 그리면 렉 걸림)
        int step = Mathf.Max(1, Mathf.Min(widthX, widthY) / 100);

        for (int x = 0; x < widthX; x += step)
        {
            for (int y = 0; y < widthY; y += step)
            {
                int nodeIndex = territory[x, y];
                float height = 0f;

                // 높이 값 가져오기
                if (_useHeightVisualization && heightWorld != null)
                {
                    height = heightWorld[x, y];
                }

                if (nodeIndex < 0) // 바다/벽
                {
                    Gizmos.color = new Color(0, 0, 1, 0.3f); // 기본 파란색 반투명

                    // 디버그 모드일 때는 바다의 Weight도 시각화 가능 (보통 0)
                    if (_influenceDebugType == InfluenceDebugType.CoastlineWeight && logicData.CoastlineDataWorld != null)
                        Gizmos.color = Color.black;
                    else if (_influenceDebugType == InfluenceDebugType.RegionEdgeWeight && logicData.RegionEdgeDataWorld != null)
                        Gizmos.color = Color.black;
                }
                else if (nodeIndex < graphData.Nodes.Count)
                {
                    if (_influenceDebugType == InfluenceDebugType.None)
                    {
                        // 기존 로직: 노드 색상
                        var ownerNode = graphData.Nodes[nodeIndex];
                        Gizmos.color = ownerNode.RegionData != null ? ownerNode.BiomeData.DebugColor : Color.gray;
                    }
                    else if (_influenceDebugType == InfluenceDebugType.CoastlineWeight && logicData.CoastlineDataWorld != null)
                    {
                        // 해안선 히트맵: 해안가(검은색 0) -> 내륙 깊숙한 곳(흰색 1.0+)
                        float weight = logicData.CoastlineDataWorld[x, y].Weight;
                        // Weight가 1.2 등 초과일 수도 있으니 정규화 시각화 혹은 클램프
                        Gizmos.color = Color.LerpUnclamped(Color.black, Color.white, weight);
                    }
                    else if (_influenceDebugType == InfluenceDebugType.RegionEdgeWeight && logicData.RegionEdgeDataWorld != null)
                    {
                        // 영토 경계(Edge) 히트맵: 경계선(검은색 0) -> 지역 중심(빨간색 1.0)
                        float weight = logicData.RegionEdgeDataWorld[x, y].Weight;
                        Gizmos.color = Color.Lerp(Color.black, Color.red, weight);
                    }
                }

                float worldX = x;
                float worldZ = y;
                float worldY = height;

                Vector3 cubePos = new Vector3(worldX, worldY, worldZ);

                // Y= -0.1f에 바닥처럼 그림
                Gizmos.DrawCube(cubePos, Vector3.one * step);
            }
        }
    }
    #endregion
#endif
}

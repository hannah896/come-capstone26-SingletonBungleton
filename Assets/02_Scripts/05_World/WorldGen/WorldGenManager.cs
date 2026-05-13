using Cysharp.Threading.Tasks;
using System;
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
    [SerializeField] private WorldSettings _worldSettings;

    [SerializeField] private WorldSimulationManager _simulationManager;

    [SerializeField] private GameObject _demoPlayer;

    [SerializeField] private int _currentSeed = 0;            // 현재 유지 중인 시드값

    private string _worldSettingLabel = "TestWorldSettings";

    private CancellationTokenSource _cts;
    private bool _isWorldSettingsLoaded;
    private bool _isStartedWorldGeneration;

    public WorldGraphDirector GraphDirector => _worldGraphDirector;
    public WorldSettings WorldSettings => _worldSettings;

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        _cts = new CancellationTokenSource();

        // 3개의 디렉터 모두 컴포넌트 유무 확인 및 부착
        if (_worldGraphDirector == null)
            _worldGraphDirector = Extensions.GetOrAddComponent<WorldGraphDirector>(this.gameObject);
        if (_worldRenderDirector == null)
            _worldRenderDirector = Extensions.GetOrAddComponent<WorldRenderDirector>(this.gameObject);
        if (_worldChunkDirector == null)
            _worldChunkDirector = Extensions.GetOrAddComponent<WorldChunkDirector>(this.gameObject);

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

    public UniTask GenerateWorldFromUI(WorldBranchSetting branch, WorldLoopSetting loop)
    {
        return GenerateWorldWithSettings(() =>
        {
            _currentSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            _worldSettings.WorldSeed = _currentSeed;
            _worldSettings.WorldSize = WorldSize.Large;
            _worldSettings.WorldBranch = branch;
            _worldSettings.WorldLoop = loop;

            Debug.Log($"UI 옵션으로 월드 생성. 시드: {_currentSeed}, Branch: {branch}, Loop: {loop}");
        });
    }

    private async UniTask GenerateWorldWithSettings(Action applySettings)
    {
        _isStartedWorldGeneration = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        if (!_isWorldSettingsLoaded || _worldSettings == null)
        {
            Debug.LogWarning("WorldSettings가 아직 로드되지 않았습니다.");
            return;
        }

        try
        {
            Debug.Log("=== 월드 생성 시작 ===");

            applySettings?.Invoke();

            // ==========================================================
            // 1단계: 논리 데이터 생성 및 청크 분할
            // ==========================================================
            await _worldGraphDirector.GenerateWorldLogicWithSettings(_worldSettings, _cts.Token);
            Debug.Log($"월드 그래프 생성 완료. 시드: {_worldSettings.WorldSeed}");

            var graphData = _worldGraphDirector.GetWorldGraphData();
            var logicData = _worldGraphDirector.GetWorldLogicData();

            // ==========================================================
            // 2단계: 렌더 디렉터 초기화 및 에셋 로드 
            // ==========================================================
            _worldRenderDirector.ClearAllChunks();
            await _worldRenderDirector.InitializeAsync(_worldSettings, graphData, _cts.Token);

            // ==========================================================
            // 3단계: 플레이어 스폰 좌표 계산
            // ==========================================================
            Vector2Int mapSize = _worldSettings.GetWorldSize();
            int startingX = Mathf.RoundToInt(mapSize.x * 0.5f);
            int startingZ = Mathf.RoundToInt(mapSize.y * 0.5f);

            bool foundStartRegion = false;

            foreach (var node in graphData.Nodes)
            {
                if (node.RegionData.RegionName.Contains("Start"))
                {
                    if (node.OwnedTiles.Count > 0)
                    {
                        long sumX = 0;
                        long sumY = 0;
                        foreach (Vector2Int tile in node.OwnedTiles)
                        {
                            sumX += tile.x;
                            sumY += tile.y;
                        }
                        startingX = (int)(sumX / node.OwnedTiles.Count);
                        startingZ = (int)(sumY / node.OwnedTiles.Count);
                        foundStartRegion = true;
                        break;
                    }
                }
            }

            if (!foundStartRegion)
            {
                Debug.LogWarning("🚨 StartRegion을 찾지 못해 맵 중앙 좌표를 사용합니다.");
            }
            else
            {
                Debug.Log($"✅ StartRegion 탐색 성공! 스폰 타일 좌표: ({startingX}, {startingZ})");
            }

            Vector2Int spawnChunkCoord = logicData.GetChunkCoord(startingX, startingZ);

            // ==========================================================
            // 4단계: 플레이어 탐색 및 StartRegion으로 
            // ==========================================================
            if (_demoPlayer == null)
            {
                foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (rootObj.CompareTag("Player"))
                    {
                        _demoPlayer = rootObj;
                        break;
                    }
                }
            }

            if (_demoPlayer != null)
            {
                _demoPlayer.transform.position = new Vector3(startingX, 10f, startingZ);
                _demoPlayer.SetActive(true);

                _worldChunkDirector.SetTarget(_demoPlayer.transform);
                Debug.Log("🎯 플레이어 StartRegion 자동 탐색 및 안전 스폰 완료!");
            }
            else
            {
                Debug.LogWarning("🚨 하이어라키에 'Player' 태그를 가진 오브젝트가 없습니다!");
            }

            // ==========================================================
            // 5단계: 청크 디렉터 초기화 및 스폰 지역 확정 렌더링 대기
            // ==========================================================
            _worldChunkDirector.Initialize(logicData, _worldRenderDirector);

            await _worldChunkDirector.LoadInitialSpawnAreaAsync(spawnChunkCoord);
            Debug.Log("월드 생성이 완료되었습니다!");

            _simulationManager = Extensions.GetOrAddComponent<WorldSimulationManager>(this.gameObject);
            _simulationManager.Initialize(_worldChunkDirector);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("맵 생성 취소됨");
        }
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
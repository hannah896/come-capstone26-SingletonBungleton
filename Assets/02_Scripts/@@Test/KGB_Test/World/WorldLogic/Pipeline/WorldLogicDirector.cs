using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WorldLogicDirector : MonoBehaviour
{
    private WorldSettings _worldSettings;
    private StoryData _storyData;

    #region Worker Fields
    private StoryGenerator _storyGenerator;
    private RegionGenerator _regionGenerator;
    private ForceSimulator _forceSimulator;
    private TerritoryBuilder _territoryBuilder;
    private HeightBuilder _heightBuilder;
    private ObjectDisposer _objectDisposer;
    #endregion

    private CancellationToken _ct;

    #region Internal Data
    private WorldGraphData _worldGraphData;
    private WorldLogicData _worldLogicData;
    private List<DisposeData> _worldDisposeDatas;
    #endregion 
    // 서비스 클래스들


    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        
        InitializeServices();
    }

    private void InitializeServices()
    {
        _storyGenerator = new();
        _regionGenerator = new();
        _forceSimulator = new();
        _territoryBuilder = new();
        _heightBuilder = new();
        _objectDisposer = new();    
    }

    #region Public API
    public async UniTask GenerateWorldLogicWithSettings(WorldSettings settings, CancellationToken ct)
    {
        _worldSettings = settings;
        _storyData = _worldSettings.CurrentStory;
        
        if (_storyData == null)
        {
            Debug.LogError("Story Data 가 WorldSettings에 설정되지 않았습니다!");
            return;
        }
        _ct = ct;

        await GenerateWorldLogicAsync(_ct);
    }
    #endregion

    #region Main Pipeline - 전체 흐름 관리
    public async UniTask GenerateWorldLogicAsync(CancellationToken ct)
    {
        if (_worldSettings == null || _storyData == null)
        {
            Debug.LogError("WorldSettings 또는 StoryData가 설정되지 않았습니다!");
            return;
        }

        InitializeServices();
        ClearWorld();

        try
        {
            Debug.Log($"=== 월드 생성 시작: {_storyData.StoryName}===");
            Debug.Log($" 전체 타일 개수 : {_worldSettings.GetTileGridSize()}, Branch: {_worldSettings.WorldBranch}, Loop: {_worldSettings.WorldLoop}");

            _worldGraphData = new WorldGraphData();

            Vector2Int gridSize = _worldSettings.GetTileGridSize();
            _worldLogicData = new WorldLogicData(gridSize);

            // 1단계: Region Graph 생성 (StoryGenerator에 위임)
            await GenerateStoryAsync(ct);
            Debug.Log($"1단계 완료: Region Graph 생성 ({_worldGraphData.Nodes.Count})");
            
            // 2단계: Convert Rooms (RegionGenerator에 위임. 각 Task들의 Room 생성
            await ConvertRegionToRoomsAsync(ct);
            Debug.Log($"2단계 완료: Region 변환 ({_worldGraphData.Nodes.Count}개 Room)");
            // 각 region에 몇개의 room이 배치되었는지 디버그
            //Debug.Log($"Region별 Room 개수: " + string.Join(", ", _worldGraphData.Nodes.Select(n => $"{n.RegionData.RegionName}: {n.Rooms.Count}개")));

            // 3단계 : Force Simulation (ForceSimulator에 위임. 노드 위치 조정)
            await ForceSimulationAsync(ct);
            Debug.Log($"3단계 완료: Force Simulation 완료");

            // 4단계: 보로노이 분할 
            await TerritoryBuildAsync(ct);
            Debug.Log($"4단계 완료: 보로노이 분할 완료");

            // 5단계: 노이즈, 높이, 경계 처리
            await HeightBuildAsync(ct);
            Debug.Log($"5단계 완료: 타일 디테일 처리 완료");

            // 6단계: 오브젝트 배치 (WorldObjectDisposer에 위임)
            await SpawnObjectsAsync(ct);
            int disposeCount = _worldDisposeDatas != null ? _worldDisposeDatas.Count : 0;
            Debug.Log($"6단계 완료: {disposeCount}개 오브젝트 배치");
            
            Debug.Log($"=== 월드 생성 완료: 시드 {_worldSettings.WorldSeed} ===");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("월드 생성이 취소되었습니다.");
            ClearWorld();
        }
    }
    #endregion

    #region 1단계: Story 생성
    private async UniTask GenerateStoryAsync(CancellationToken ct)
    {
        // StoryGenerator에게 Story 생성 위임 (_storyData 전달 후 완료되면 갱신함)
        _worldGraphData = await _storyGenerator.GenerateStoryAsync(_worldGraphData, _worldSettings, ct);

    }
    #endregion

    #region 2단계: Region을 Room들의 집합으로 변환
    private async UniTask ConvertRegionToRoomsAsync(CancellationToken ct)
    {
        // RegionGenerator에게 Region -> Room 변환 위임(_nodes, _nodeConnections 전달 후 완료되면 갱신함)
        _worldGraphData = await _regionGenerator.ConvertRegionsToRoomsAsync(_worldGraphData, _worldSettings, ct);
        _worldGraphData.UpdateNodeIndices(); // 변환 후 인덱스 갱신
    }
    #endregion

    #region 3단계: Force Simulation
    private async UniTask ForceSimulationAsync(CancellationToken ct)
    {
        // ForceSimulator에게 노드 위치 조정 위임(_nodes 전달 후 완료되면 갱신함)
        _worldGraphData = await _forceSimulator.ForceSimulateAsync(_worldGraphData, _worldSettings, ct);
    }
    #endregion
    #region 4단계: 영토 분할
    private async UniTask TerritoryBuildAsync(CancellationToken ct)
    {
        _worldLogicData = await _territoryBuilder.TerritoryBuildAsync(_worldGraphData, _worldLogicData, _worldSettings, ct);
    }
    #endregion

    #region 5단계: 타일 디테일 처리 (노이즈, 높이, 경계)
    private async UniTask HeightBuildAsync(CancellationToken ct)
    {
        _worldLogicData = await _heightBuilder.HeightBuildAsync(_worldGraphData, _worldLogicData, _worldSettings, ct);
    }
    #endregion

    #region 6단계: 오브젝트 배치
    private async UniTask SpawnObjectsAsync(CancellationToken ct)
    {
        await _objectDisposer.SpawnObjectsAsync(_worldGraphData, _worldSettings, ct);
    }

    #endregion


    private void ClearWorld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (_worldGraphData != null)
        {
            if (_worldGraphData.Nodes == null) _worldGraphData.Nodes = new List<Node>();
            if (_worldGraphData.NodeConnections == null) _worldGraphData.NodeConnections = new List<NodeConnection>();
            if (_worldGraphData.AdjacencyList == null) _worldGraphData.AdjacencyList = new Dictionary<Node, List<Node>>();

            _worldGraphData.Clear();
        }
    }

    #region Debug & Gizmos
    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _showConnections = true;
    [SerializeField] private bool _showMapBounds = true;
    [SerializeField] private bool _drawTerritoryGrid = false;
    [SerializeField] private bool _useHeightVisualization = true;
    [SerializeField] private bool _showRegionNames = true; // ★ 텍스트 표시 토글 추가

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _worldGraphData == null || _worldGraphData.Nodes == null || _worldSettings == null) return;

        if (_showMapBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_worldSettings.GetRealWorldSize().x / 2f, 0, _worldSettings.GetRealWorldSize().y / 2f);
            Vector3 size = new Vector3(_worldSettings.GetRealWorldSize().x, 0, _worldSettings.GetRealWorldSize().y);
            Gizmos.DrawWireCube(center, size);
        }

        int tileSize = _worldSettings.TileUnitSize;
        float halfTile = tileSize / 2f;

        // 1. 노드 중심점 그리기
        foreach (var node in _worldGraphData.Nodes)
        {
            float worldX = (node.Position.x * tileSize) + halfTile;
            float worldZ = (node.Position.y * tileSize) + halfTile;
            Vector3 nodePos = new Vector3(worldX, 0, worldZ);

            if (_useHeightVisualization && _worldLogicData?.HeightWorld != null)
            {
                int tileX = Mathf.RoundToInt(node.Position.x);
                int tileY = Mathf.RoundToInt(node.Position.y);
                float height = _worldLogicData.GetHeightAt(tileX, tileY);
                nodePos.y = height;
            }

            Gizmos.color = node.RoomData != null ? node.RoomData.DebugColor : Color.white;
            Gizmos.DrawSphere(nodePos, tileSize * 0.8f);

#if UNITY_EDITOR
            if (_showRegionNames && node.RegionData != null)
            {
                // 텍스트 스타일(색상, 폰트 크기, 정렬 등) 설정
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.black; // 글자색 (배경이 밝으면 black 추천)
                style.fontSize = 12;                  // 폰트 크기
                style.fontStyle = FontStyle.Bold;     // 볼드체
                style.alignment = TextAnchor.MiddleCenter;

                // 노드보다 살짝 위쪽에 텍스트 배치 (높이는 조절 가능)
                Vector3 labelPos = nodePos + Vector3.up * (tileSize * 1.5f);

                // 화면에 텍스트 렌더링
                UnityEditor.Handles.Label(labelPos, $"{ node.RegionData.RegionName}: {node.RoomDepth}", style);
            }
#endif
        }

        // 2. 연결선 그리기
        if (_worldGraphData.NodeConnections != null)
        {
            Gizmos.color = Color.white;
            foreach (var conn in _worldGraphData.NodeConnections)
            {
                if (conn.ParentNode == null || conn.ChildNode == null) continue;
                float startX = (conn.ParentNode.Position.x * tileSize) + halfTile;
                float startZ = (conn.ParentNode.Position.y * tileSize) + halfTile;
                float endX = (conn.ChildNode.Position.x * tileSize) + halfTile;
                float endZ = (conn.ChildNode.Position.y * tileSize) + halfTile;

                Vector3 start = new Vector3(startX, 0, startZ);
                Vector3 end = new Vector3(endX, 0, endZ);

                // 높이 적용
                if (_useHeightVisualization && _worldLogicData?.HeightWorld != null)
                {
                    int startTileX = Mathf.RoundToInt(conn.ParentNode.Position.x);
                    int startTileY = Mathf.RoundToInt(conn.ParentNode.Position.y);
                    int endTileX = Mathf.RoundToInt(conn.ChildNode.Position.x);
                    int endTileY = Mathf.RoundToInt(conn.ChildNode.Position.y);

                    start.y = _worldLogicData.GetHeightAt(startTileX, startTileY);
                    end.y = _worldLogicData.GetHeightAt(endTileX, endTileY);
                }

                Gizmos.DrawLine(start, end);
            }
        }

        // 3. 영토 그리드(보로노이) 그리기
        if (_drawTerritoryGrid && _worldLogicData.TerritoryWorld != null)
        {
            DrawTerritoryGizmos();
        }
    }

    
    private void DrawTerritoryGizmos()
    {
        int[,] territory = _worldLogicData.TerritoryWorld;
        float[,] heightWorld = _worldLogicData.HeightWorld;
        int widthX = territory.GetLength(0);
        int widthY = territory.GetLength(1);

        int tileSize = _worldSettings.TileUnitSize;
        int tileHeight = _worldSettings.TileUnitHeight;

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
                    Gizmos.color = new Color(0, 0, 1, 0.3f); // 파란색 반투명
                }
                else if (nodeIndex < _worldGraphData.Nodes.Count)
                {
                    // 해당 타일의 주인(Node)의 색상 가져오기
                    var ownerNode = _worldGraphData.Nodes[nodeIndex];
                    Color c = ownerNode.RoomData != null ? ownerNode.RoomData.DebugColor : Color.gray;
                    
                    Gizmos.color = c;
                }

                float worldX = x * tileSize + tileSize / 2f;
                float WorldZ = y * tileSize + tileSize / 2f;

                float worldY = height * tileHeight;

                Vector3 cubePos = new Vector3(worldX, worldY, WorldZ);

                // Y= -0.1f에 바닥처럼 그림
                Gizmos.DrawCube(cubePos, Vector3.one * tileSize * step);
            }
        }
    }
    #endregion

    #region Getters
    public WorldLogicData GetWorldLogicData() => _worldLogicData;
    public WorldGraphData GetWorldGraphData() => _worldGraphData;   
    public List<DisposeData> GetWorldDisposeDatas() => _worldDisposeDatas;
    #endregion

}



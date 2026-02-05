using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    private WorldSettings _worldSettings;
    private StoryData _storyData;
    private StoryGenerator _storyGenerator;
    private RegionGenerator _regionGenerator;
    private TilePartitioner _tilePartitioner;
    private WorldObjectDisposer _objectDisposer;

    public int seed = 0;
    public bool useRandomSeed = true;

    private CancellationTokenSource _cts;
    
    // 생성된 데이터
    private GraphResult _result;
    
    // 서비스 클래스들


    async void Start()
    {
        await UniTask.WaitUntil(() => Main.Instance != null);
        _cts = new CancellationTokenSource();
        
        if (_storyGenerator == null)
            _storyGenerator = GetComponent<StoryGenerator>();
        
        InitializeServices();
    }

    private void InitializeServices()
    {
        _storyGenerator = new();
        _regionGenerator = new();
        _tilePartitioner = new();
        _objectDisposer = new();
        if (_result == null)
        {
            _result = new GraphResult();
            _result.Nodes = new List<Node>();
            _result.NodeConnections = new List<NodeConnection>();
            _result.AdjacencyList = new Dictionary<Node, List<Node>>();
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    #region Public API
    public void GenerateWorldWithSettings(WorldSettings settings)
    {
        _worldSettings = settings;
        _storyData = _worldSettings.CurrentStory;
        
        if (_storyData == null)
        {
            Debug.LogError("Story Data 가 WorldSettings에 설정되지 않았습니다!");
            return;
        }
        
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        
        GenerateWorldAsync(_cts.Token).Forget();
    }
    #endregion

    #region Main Pipeline - 전체 흐름 관리
    public async UniTask GenerateWorldAsync(CancellationToken ct = default)
    {
        if (_worldSettings == null || _storyData == null)
        {
            Debug.LogError("WorldSettings 또는 StoryData가 설정되지 않았습니다!");
            return;
        }

        // 시드 초기화
        seed = useRandomSeed ? System.DateTime.Now.GetHashCode() : Mathf.Abs(seed);
        Random.InitState(seed);
        
        InitializeServices();
        ClearWorld();

        try
        {
            Debug.Log($"=== 월드 생성 시작: {_storyData.StoryName}===");
            Debug.Log($" 크기: {_worldSettings.GetWorldSize()}, Branch: {_worldSettings.WorldBranch}, Loop: {_worldSettings.WorldLoop}");

            // 1단계: Region Graph 생성 (StoryGenerator에 위임)
            await GenerateStoryAsync(ct);
            Debug.Log($"1단계 완료: Region Graph 생성 ({_result.Nodes?.Count ?? 0})");
            
            // 2단계: Convert Rooms (RegionGenerator에 위임. 각 Task들의 Room 생성
            await ConvertRegionToRoomAsync(ct);
            Debug.Log($"2단계 완료: Region 변환 ({_result.Nodes.Count}개 Room)");

            // 3단계: 보로노이 분할 및 영역 할당 (TilePartitioner에 위임)
            await PartitionWorldAsync(ct);
            Debug.Log($"3단계 완료: 맵 분할 완료");

            // 4단계: 오브젝트 배치 (WorldObjectDisposer에 위임)
            await SpawnObjectsAsync(ct);
            Debug.Log($"4단계 완료: {_objectDisposer.SpawnResults.Count}개 오브젝트 배치");
            
            Debug.Log($"=== 월드 생성 완료: 시드 {seed} ===");
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
        _result = await _storyGenerator.GenerateStoryAsync(_result, _worldSettings, ct);


    }
    #endregion

    #region 2단계: Region을 Room들의 집합으로 변환
    private async UniTask ConvertRegionToRoomAsync(CancellationToken ct)
    {
        // RegionGenerator에게 Region -> Room 변환 위임(_nodes, _nodeConnections 전달 후 완료되면 갱신함)
        _result = await _regionGenerator.ConvertRegionsToRoomsAsync(_result, _worldSettings, ct);
    }
    #endregion

    #region 3단계: 보로노이 분할 및 영역 할당 
    private async UniTask PartitionWorldAsync(CancellationToken ct)
    {
        await _tilePartitioner.PartitionWorldAsync(_result, _worldSettings, ct);
    }
    #endregion

    #region 4단계: 오브젝트 배치
    private async UniTask SpawnObjectsAsync(CancellationToken ct)
    {
        await _objectDisposer.SpawnObjectsAsync(_result, _worldSettings, ct);
    }

    #endregion


    private void ClearWorld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (_result != null)
        {
            if (_result.Nodes == null) _result.Nodes = new List<Node>();
            if (_result.NodeConnections == null) _result.NodeConnections = new List<NodeConnection>();
            if (_result.AdjacencyList == null) _result.AdjacencyList = new Dictionary<Node, List<Node>>();

            _result.Clear();
        }
    }

    #region Debug & Gizmos
    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _showConnections = true;
    [SerializeField] private bool _showMapBounds = true;
    [SerializeField] private bool _drawTerritoryGrid = false;

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _result == null || _result.Nodes == null || _worldSettings == null) return;

        if (_showMapBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_worldSettings.GetWorldSize().x / 2f, 0, _worldSettings.GetWorldSize().y / 2f);
            Vector3 size = new Vector3(_worldSettings.GetWorldSize().x, 0, _worldSettings.GetWorldSize().y);
            Gizmos.DrawWireCube(center, size);
        }

        // 1. 노드 중심점 그리기
        foreach (var node in _result.Nodes)
        {
            Gizmos.color = node.RoomData != null ? node.RoomData.DebugColor : Color.white;
            Gizmos.DrawSphere(new Vector3(node.Position.x, 0, node.Position.y), 2f);
        }

        // 2. 연결선 그리기
        if (_result.NodeConnections != null)
        {
            Gizmos.color = Color.white;
            foreach (var conn in _result.NodeConnections)
            {
                if (conn.ParentNode == null || conn.ChildNode == null) continue;
                Vector3 start = new Vector3(conn.ParentNode.Position.x, 0, conn.ParentNode.Position.y);
                Vector3 end = new Vector3(conn.ChildNode.Position.x, 0, conn.ChildNode.Position.y);
                Gizmos.DrawLine(start, end);
            }
        }

        // 3. 영토 그리드(보로노이) 그리기
        if (_drawTerritoryGrid && _tilePartitioner.TerritoryWorld != null)
        {
            DrawTerritoryGrid();
        }
    }

    
    private void DrawTerritoryGrid()
    {
        int[,] territory = _tilePartitioner.TerritoryWorld;
        int width = territory.GetLength(0);
        int height = territory.GetLength(1);

        // 성능을 위해 스텝 건너뛰기 (전체 다 그리면 렉 걸림)
        int step = Mathf.Max(1, Mathf.Min(width, height) / 100);

        for (int x = 0; x < width; x += step)
        {
            for (int y = 0; y < height; y += step)
            {
                int nodeIndex = territory[x, y];

                if (nodeIndex < 0) // 바다/벽
                {
                    Gizmos.color = new Color(0, 0, 1, 0.3f); // 파란색 반투명
                }
                else if (nodeIndex < _result.Nodes.Count)
                {
                    // 해당 타일의 주인(Node)의 색상 가져오기
                    var ownerNode = _result.Nodes[nodeIndex];
                    Color c = ownerNode.RoomData != null ? ownerNode.RoomData.DebugColor : Color.gray;
                    c.a = 0.5f; // 반투명
                    Gizmos.color = c;
                }

                // Y= -0.1f에 바닥처럼 그림
                Gizmos.DrawCube(new Vector3(x, -0.1f, y), Vector3.one * step);
            }
        }
    }
    #endregion

    #region Public Getters
    public int GetSeed() => seed;
    #endregion
}


[System.Serializable]
public class Node
{
    public int Depth = -1;
    public int RoomDepth = -1;

    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Force;

    public RegionData RegionData;
    public RoomData RoomData;


    // 소유 타일 목록
    public List<Vector2Int> OwnedTiles = new();
}

[System.Serializable]
public class NodeConnection
{
    public Node ParentNode;
    public Node ChildNode;

    public NodeConnection(Node a, Node b)
    {
        ParentNode = a;
        ChildNode = b;
    }
}

/// <summary>
/// 생성 결과
/// </summary>
[System.Serializable]
public class GraphResult
{
    private List<Node> _nodes;
    public List<Node> Nodes
    {
        get => _nodes;
        set => _nodes = value;
    }

    private List<NodeConnection> _nodeConnections;
    public List<NodeConnection> NodeConnections
    {
        get => _nodeConnections;
        set => _nodeConnections = value;
    }

    private Dictionary<Node, List<Node>> _adjacencyList;
    public Dictionary<Node, List<Node>> AdjacencyList
    {
        get => _adjacencyList;
        set => _adjacencyList = value;
    }

    public void Clear()
    {
        _nodes.Clear();
        _nodeConnections.Clear();
        _adjacencyList.Clear();
    }

    public int GetChildCount(Node parentNode)
    {
        return _nodeConnections.Count(conn => conn.ParentNode == parentNode);
    }

    public NodeConnection CreateConnection(Node parent, Node child)
    {
        if (AreConnected(parent, child)) return null;

        NodeConnection connection = new NodeConnection(parent, child);
        _nodeConnections.Add(connection);

        _adjacencyList[parent].Add(child);
        _adjacencyList[child].Add(parent);

        // 깊이 업데이트
        if (parent.Depth >= 0 && child.Depth < 0)
        {
            child.Depth = parent.Depth + 1;
        }
        else if (child.Depth >= 0 && parent.Depth < 0)
        {
            parent.Depth = child.Depth + 1;
        }

        return connection;
    }

    public bool AreConnected(Node a, Node b)
    {
        return _adjacencyList.ContainsKey(a) && _adjacencyList[a].Contains(b);
    }

    public void RebuildAdjacency()
    {
        _adjacencyList.Clear();
        foreach (var node in _nodes)
        {
            if (!_adjacencyList.ContainsKey(node))
                _adjacencyList[node] = new List<Node>();
        }

        foreach (var conn in _nodeConnections)
        {
            // 안전장치: 혹시 Nodes에 없는 노드가 연결에 있다면 건너뜀
            if (_adjacencyList.ContainsKey(conn.ParentNode) && _adjacencyList.ContainsKey(conn.ChildNode))
            {
                _adjacencyList[conn.ParentNode].Add(conn.ChildNode);
                _adjacencyList[conn.ChildNode].Add(conn.ParentNode);
            }
        }
    }
}
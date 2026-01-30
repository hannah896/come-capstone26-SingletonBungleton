using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private WorldSettings _worldSettings;
    [SerializeField] private StoryData _storyData;
    [SerializeField] private StoryGenerator _storyGenerator;
    [SerializeField] private RegionGenerator _regionGenerator;


    [Header("Partitioning Settings")]
    [SerializeField] private TilePartitioner.PartitionSettings _partitionSettings;
    
    [Header("Spawning Settings")]
    [SerializeField] private WorldObjectDisposer.SpawnSettings _spawnSettings;
    
    public int seed = 0;
    public bool useRandomSeed = true;

    private CancellationTokenSource _cts;
    
    // 생성된 데이터
    private List<Node> _nodes = new();
    private List<NodeConnection> _nodeConections = new();
    private HashSet<KeyData> _currentKeys = new();
    
    // 서비스 클래스들
    private TilePartitioner _tilePartitioner;
    private WorldObjectDisposer _objectSpawner;

    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Instance != null);
        _cts = new CancellationTokenSource();
        
        if (_storyGenerator == null)
            _storyGenerator = GetComponent<StoryGenerator>();
        
        InitializeServices();
    }

    private void InitializeServices()
    {
        _partitionSettings ??= new TilePartitioner.PartitionSettings();
        _spawnSettings ??= new WorldObjectDisposer.SpawnSettings();
        
        _tilePartitioner = new TilePartitioner(_partitionSettings);
        _objectSpawner = new WorldObjectDisposer(_spawnSettings);
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
        
        _storyGenerator.SetWorldSettings(settings);
        
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
        
        if (_partitionSettings != null)
            _partitionSettings.noiseSeed = seed;
        
        InitializeServices();
        ClearWorld();

        try
        {
            Debug.Log($"=== 월드 생성 시작: {_storyData.StoryName} ===");
            
            // 1단계: Region Graph 생성 (StoryGenerator에 위임)
            await GenerateStoryAsync(ct);
            Debug.Log($"1단계 완료: Region Graph 생성 ({_nodes?.Count ?? 0}개 Task)");
            
            // 2단계: Convert Rooms (RegionGenerator에 위임. 각 Task들의 Room 생성
            await ConvertRegionToRoom(ct);
            Debug.Log($"2단계 완료: Region 변환 ({_nodes.Count}개 Region)");

            // 3단계: 보로노이 분할 및 영역 할당 (TilePartitioner에 위임)
            await PartitionWorldAsync(ct);
            Debug.Log($"3단계 완료: 맵 분할 완료");

            // 4단계: 오브젝트 배치 (WorldObjectDisposer에 위임)
            await SpawnObjectsAsync(ct);
            Debug.Log($"4단계 완료: {_objectSpawner.SpawnResults.Count}개 오브젝트 배치");
            
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
        GraphResult regionResult = await _storyGenerator.GenerateStoryAsync(_storyData, ct);
        _nodes = regionResult.nodes;
        _nodeConections = regionResult.nodeConnections;

    }
    #endregion

    #region 2단계: Region을 Room들의 집합으로 변환
    private async Task ConvertRegionToRoom(CancellationToken ct)
    {
        // RegionGenerator에게 Region -> Room 변환 위임(_nodes, _nodeConnections 전달 후 완료되면 갱신함)
        GraphResult roomResult = await _regionGenerator.ConvertRegionsToRoomsAsync(_nodes, _nodeConections, ct);
        _nodes = roomResult.nodes;
        _nodeConections = roomResult.nodeConnections;
    }
    #endregion

    #region 3-4단계: 기존 로직 유지
    private async UniTask PartitionWorldAsync(CancellationToken ct)
    {
        if (_taskRegions.Count == 0)
        {
            Debug.LogWarning("분할할 영역이 없습니다.");
            return;
        }

        Vector2Int worldSize = _worldSettings.GetWorldSize();
        NormalizeRegionCenters(worldSize);
        await _tilePartitioner.PartitionWorldAsync(worldSize, _taskRegions, ct);
        
        int totalTiles = _taskRegions.Sum(r => r.ownedTiles?.Count ?? 0);
        Debug.Log($"맵 분할 완료: 총 {totalTiles}개 타일 할당됨");
    }

    private void NormalizeRegionCenters(Vector2Int worldSize)
    {
        if (_taskRegions.Count == 0) return;

        if (_taskRegions.Count == 1)
        {
            _taskRegions[0].center = new Vector2(worldSize.x * 0.5f, worldSize.y * 0.5f);
            return;
        }

        float minX = _taskRegions.Min(r => r.center.x);
        float maxX = _taskRegions.Max(r => r.center.x);
        float minY = _taskRegions.Min(r => r.center.y);
        float maxY = _taskRegions.Max(r => r.center.y);

        float rangeX = maxX - minX;
        float rangeY = maxY - minY;

        float padding = 0.1f;
        float usableWidth = worldSize.x * (1f - padding * 2);
        float usableHeight = worldSize.y * (1f - padding * 2);

        foreach (var region in _taskRegions)
        {
            float normalizedX = rangeX > 0 ? (region.center.x - minX) / rangeX : 0.5f;
            float normalizedY = rangeY > 0 ? (region.center.y - minY) / rangeY : 0.5f;

            region.center = new Vector2(
                padding * worldSize.x + normalizedX * usableWidth,
                padding * worldSize.y + normalizedY * usableHeight
            );
        }
    }

    private async UniTask SpawnObjectsAsync(CancellationToken ct)
    {
        if (_taskRegions.Count == 0) return;

        float densityMultiplier = _worldSettings.DensityMultiplier;
        await _objectSpawner.SpawnObjectsAsync(_taskRegions, densityMultiplier, ct);
    }
    #endregion

    private void ClearWorld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        
        _nodes.Clear();
    }

    #region Debug & Gizmos
    [Header("Debug")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _drawTerritoryGrid = false;

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _taskRegions == null || _taskRegions.Count == 0) return;

        // 영역 중심점 그리기
        foreach (var region in _taskRegions)
        {
            Gizmos.color = region.isMainStory ? Color.green : Color.yellow;
            if (region.room != null)
                Gizmos.color = region.room.debugColor;
            
            Gizmos.DrawSphere(new Vector3(region.center.x, 0, region.center.y), 2f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                new Vector3(region.center.x, 2f, region.center.y),
                $"{region.task.taskName}\nD:{region.depth} T:{region.ownedTiles?.Count ?? 0}"
            );
            #endif
        }

        // Story 연결선 그리기
        if (_storyResult?.taskConnections != null)
        {
            Gizmos.color = Color.white;
            foreach (var conn in _storyResult.taskConnections)
            {
                if (conn?.taskA == null || conn?.taskB == null) continue;
                
                Vector3 start = new Vector3(conn.taskA.centerPosition.x, 1f, conn.taskA.centerPosition.y);
                Vector3 end = new Vector3(conn.taskB.centerPosition.x, 1f, conn.taskB.centerPosition.y);
                Gizmos.DrawLine(start, end);
            }
        }

        // 영토 그리드 그리기
        if (_drawTerritoryGrid && _tilePartitioner?.TerritoryWorld != null)
        {
            DrawTerritoryGrid();
        }
    }

    private void DrawTerritoryGrid()
    {
        int[,] territory = _tilePartitioner.TerritoryWorld;
        int width = territory.GetLength(0);
        int height = territory.GetLength(1);

        int step = Mathf.Max(1, Mathf.Min(width, height) / 50);

        for (int x = 0; x < width; x += step)
        {
            for (int y = 0; y < height; y += step)
            {
                int regionId = territory[x, y];
                if (regionId < 0)
                {
                    Gizmos.color = Color.blue * 0.5f;
                }
                else if (regionId < _taskRegions.Count)
                {
                    Gizmos.color = _taskRegions[regionId].room?.debugColor ?? Color.gray;
                }
                
                Gizmos.DrawCube(new Vector3(x, -0.5f, y), Vector3.one * step * 0.8f);
            }
        }
    }
    #endregion

    #region Public Getters
    public TilePartitioner GetTilePartitioner() => _tilePartitioner;
    public ObjectSpawner GetObjectSpawner() => _objectSpawner;
    public int GetSeed() => seed;
    #endregion
}

#region Data Classes
[System.Serializable]
public class Node
{
    public int Depth;
    public Vector2 Position;

    public RegionData RegionData;
    public RoomData RoomData;

    public Vector2 Velocity { get; set; }
    public Vector2 Force { get; set; }

    // 소유 타일 목록
    public List<Vector2Int> OwnedTiles = new();
    
    // 바운딩 박스 (최적화용)
    public Bounds GetBounds()
    {
        if (OwnedTiles == null || OwnedTiles.Count == 0)
            return new Bounds(new Vector3(Position.x, 0, Position.y), Vector3.zero);

        int minX = OwnedTiles.Min(tile => tile.x);
        int maxX = OwnedTiles.Max(tile => tile.x);
        int minY = OwnedTiles.Min(tile => tile.y);
        int maxY = OwnedTiles.Max(tile => tile.y);

        Vector3 boundsCenter = new Vector3((minX + maxX) / 2f, 0, (minY + maxY) / 2f);
        Vector3 boundsSize = new Vector3(maxX - minX, 1, maxY - minY);
        
        return new Bounds(boundsCenter, boundsSize);
    }
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
public class GraphResult
{
    public List<Node> nodes;
    public List<NodeConnection> nodeConnections;
}
#endregion
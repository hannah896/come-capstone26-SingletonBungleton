using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class WorldGraphDirector : MonoBehaviour
{
    private WorldSettings _worldSettings;
    private StoryData _storyData;

    #region Worker Fields
    private List<IGraphPipelineStage> _pipeline;
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
        _pipeline = new List<IGraphPipelineStage>()
        {
            new StoryGenerator(),
            new RegionGenerator(),
            new ForceSimulator(),
        };
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

            _worldGraphData = new WorldGraphData();

            Vector2Int worldSize = _worldSettings.GetWorldSize();
            _worldLogicData = new WorldLogicData(worldSize);

            // 1~3단계: Story 생성, Region->Room 변환, Force Simulation을 순차적으로 실행하는 파이프라인
            foreach (var stage in _pipeline)
            {
                stage.Initialize(_worldSettings);
                _worldGraphData = await stage.ExecuteAsync(_worldGraphData, ct);
                Debug.Log($"{stage.GetType().Name} 완료");
            }


            /*
            Debug.Log($"=== 글로벌 뼈대 완성 (노드 수: {_worldGraphData.Nodes.Count}) ===");

            // startRegion의 청크 좌표 계산
            Vector2 startRegionPos = _worldGraphData.Nodes[0].Position; // 예시로 첫 번째 노드를 시작점으로 사용
            int startChunkX = Mathf.FloorToInt(startRegionPos.x / _worldLogicData.ChunkSize);
            int startChunkY = Mathf.FloorToInt(startRegionPos.y / _worldLogicData.ChunkSize);

            //TODO: ChunkStreamingManager 에 넘겨서 플레이어 주변 청크만 생성하도록 하기
            Vector2Int startChunkPos = new Vector2Int(startChunkX, startChunkY);

            // 중앙의 3x3 (9개) 청크만 먼저 생성
            await GenerateChunksAreaAsync(startChunkPos, 1, ct);
            */

            // 4단계: 그래프 노드들을 기반으로 보로노이 분할하여 영토 할당 
            await TerritoryBuildAsync(ct);
            Debug.Log($"4단계 완료: 보로노이 분할 완료");

            // 5단계: 노이즈, 높이, 경계 처리
            await HeightBuildAsync(ct);
            Debug.Log($"5단계 완료: 타일 디테일 처리 완료");

            // 6단계: 오브젝트 배치 (WorldObjectDisposer에 위임)
            await SpawnObjectsAsync(ct);
            int disposeCount = _worldDisposeDatas != null ? _worldDisposeDatas.Count : 0;
            Debug.Log($"6단계 완료: {disposeCount}개 오브젝트 배치");

            Debug.Log($"World Logic 생성 완료!");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("월드 생성이 취소되었습니다.");
            ClearWorld();
        }
    }
    #endregion
    /// <summary>
    /// TODO: 4 ~ 6단계 Chunk Director에 로컬화 => 특정 청크 주변 영역에 대해서만 보로노이 분할, 높이 맵 생성, 오브젝트 배치를 수행, 청크 디렉터로 분리
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// 
    public async UniTask GenerateChunksAreaAsync(Vector2Int centerChunk, int radius, CancellationToken ct)
    {
        for (int cx = centerChunk.x - radius; cx <= centerChunk.x + radius; cx++)
        {
            for (int cy = centerChunk.y - radius; cy <= centerChunk.y + radius; cy++)
            {
                Vector2Int chunkCoord = new Vector2Int(cx, cy);
                ChunkData chunkData = _worldLogicData.GetOrCreateChunk(chunkCoord);

                // TODO: 4~6단계를 이 ChunkData 단위로 실행하도록 넘겨줍니다.
                // 4단계: 영향력 맵 생성 (기존 TerritoryBuilder 개편)
                // await _territoryBuilder.BuildChunkInfluenceAsync(chunkData, _worldGraphData, _worldSettings, ct);

                // 5단계: 노이즈 및 고도 적용 (기존 HeightBuilder 개편)
                // await _heightBuilder.BuildChunkHeightAsync(chunkData, _worldGraphData, _worldSettings, ct);

                // 6단계: 오브젝트 배치
                // await _objectDisposer.SpawnObjectsInChunkAsync(chunkData, _worldGraphData, _worldSettings, ct);
            }
        }
        Debug.Log($"청크 베이킹 완료! (중심: {centerChunk}, 반경: {radius})");
    }

    #region 4단계: 영토 분할
    private async UniTask TerritoryBuildAsync(CancellationToken ct)
    {
        _worldLogicData = await _territoryBuilder.TerritoryBuildAsync(_worldGraphData, _worldLogicData, _worldSettings, ct);
    }
    #endregion

    #region 5단계: 높이 맵 생성
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

    public enum InfluenceDebugType { None, CoastlineWeight, RegionEdgeWeight }
    [Header("Heatmap Mode")]
    [Tooltip("그리드(TerritoryGrid) 렌더링 시 어떤 데이터를 시각화할지 선택합니다.")]
    [SerializeField] private InfluenceDebugType _influenceDebugType = InfluenceDebugType.None;

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _worldGraphData == null || _worldGraphData.Nodes == null || _worldSettings == null) return;

        if (_showMapBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_worldSettings.GetWorldSize().x / 2f, 0, _worldSettings.GetWorldSize().y / 2f);
            Vector3 size = new Vector3(_worldSettings.GetWorldSize().x, 0, _worldSettings.GetWorldSize().y);
            Gizmos.DrawWireCube(center, size);
        }


        // 1. 노드 중심점 그리기
        foreach (var node in _worldGraphData.Nodes)
        {
            float worldX = node.Position.x;
            float worldZ = node.Position.y;
            Vector3 nodePos = new Vector3(worldX, 0, worldZ);

            if (_useHeightVisualization && _worldLogicData?.HeightWorld != null)
            {
                int tileX = Mathf.RoundToInt(node.Position.x);
                int tileY = Mathf.RoundToInt(node.Position.y);
                float height = _worldLogicData.GetHeightAt(tileX, tileY);
                nodePos.y = height;
            }

            Gizmos.color = node.RoomData != null ? node.RoomData.DebugColor : Color.white;
            Gizmos.DrawSphere(nodePos, 1.0f);

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
                Vector3 labelPos = nodePos + Vector3.up * 1.5f;

                // 화면에 텍스트 렌더링
                UnityEditor.Handles.Label(labelPos, $"{ node.RegionData.RegionName}: {node.RoomDepth}, x:{nodePos.x} y:{nodePos.y} z:{nodePos.z}", style);
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
                var parentNode = conn.ParentNode;
                var childNode = conn.ChildNode;

                float startX = parentNode.Position.x;
                float startZ = parentNode.Position.y;
                float endX = childNode.Position.x;
                float endZ = childNode.Position.y;

                Vector3 start = new Vector3(startX, 0, startZ);
                Vector3 end = new Vector3(endX, 0, endZ);

                // 높이 적용
                if (_useHeightVisualization && _worldLogicData?.HeightWorld != null)
                {
                    int startTileX = Mathf.RoundToInt(parentNode.Position.x);
                    int startTileY = Mathf.RoundToInt(parentNode.Position.x);
                    int endTileX = Mathf.RoundToInt(childNode.Position.x);
                    int endTileY = Mathf.RoundToInt(childNode.Position.y);

                    start.y = _worldLogicData.GetHeightAt(startTileX, startTileY);
                    end.y = _worldLogicData.GetHeightAt(endTileX, endTileY);
                }

                if(parentNode.RoomData != null && childNode.RoomData != null)
                {
                    // 연결된 두 노드의 Room 색상 평균으로 선 색상 설정
                    Color startColor = parentNode.RoomData.DebugColor;
                    Color endColor = childNode.RoomData.DebugColor;
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
                    if (_influenceDebugType == InfluenceDebugType.CoastlineWeight && _worldLogicData.CoastlineDataWorld != null)
                        Gizmos.color = Color.black;
                    else if (_influenceDebugType == InfluenceDebugType.RegionEdgeWeight && _worldLogicData.RegionEdgeDataWorld != null)
                        Gizmos.color = Color.black;
                }
                else if (nodeIndex < _worldGraphData.Nodes.Count)
                {
                    if (_influenceDebugType == InfluenceDebugType.None)
                    {
                        // 기존 로직: 노드 색상
                        var ownerNode = _worldGraphData.Nodes[nodeIndex];
                        Gizmos.color = ownerNode.RoomData != null ? ownerNode.RoomData.DebugColor : Color.gray;
                    }
                    else if (_influenceDebugType == InfluenceDebugType.CoastlineWeight && _worldLogicData.CoastlineDataWorld != null)
                    {
                        // 해안선 히트맵: 해안가(검은색 0) -> 내륙 깊숙한 곳(흰색 1.0+)
                        float weight = _worldLogicData.CoastlineDataWorld[x, y].Weight;
                        // Weight가 1.2 등 초과일 수도 있으니 정규화 시각화 혹은 클램프
                        Gizmos.color = Color.LerpUnclamped(Color.black, Color.white, weight);
                    }
                    else if (_influenceDebugType == InfluenceDebugType.RegionEdgeWeight && _worldLogicData.RegionEdgeDataWorld != null)
                    {
                        // 영토 경계(Edge) 히트맵: 경계선(검은색 0) -> 지역 중심(빨간색 1.0)
                        float weight = _worldLogicData.RegionEdgeDataWorld[x, y].Weight;
                        Gizmos.color = Color.Lerp(Color.black, Color.red, weight);
                    }
                }

                float worldX = x;
                float WorldZ = y;
                float worldY = height;

                Vector3 cubePos = new Vector3(worldX, worldY, WorldZ);

                // Y= -0.1f에 바닥처럼 그림
                Gizmos.DrawCube(cubePos, Vector3.one * step);
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



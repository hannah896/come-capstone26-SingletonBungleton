using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 역할: 게임 시작(혹은 맵 생성) 시 딱 한 번만 실행되어 맵의 전체 구조를 만듭니다.
///
/// 실행 파이프라인: 1~5단계(지형 / 오브젝트 생성)-> 6단계(ChunkSlicer)
///
/// 출력물: 수백 개의 ChunkData가 예쁘게 담겨 있는 WorldLogicData.
/// </summary>
public class WorldGraphDirector : MonoBehaviour
{
    private WorldSettings _worldSettings;
    private StoryData _storyData;

    #region Worker Fields
    private List<IGraphPipelineStage> _pipeline;
    #endregion

    private CancellationToken _ct;

    #region Internal Data
    private WorldGenContext _currentContext;
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
            new ForceSimulator(),
            new TerritoryBuilder(),
            new HeightBuilder(),
            new ObjectDisposer(),
            new ChunkSlicer()
        };
    }

    #region public methods
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
    private async UniTask GenerateWorldLogicAsync(CancellationToken ct)
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

            _currentContext = new WorldGenContext
            {
                Settings = _worldSettings,
                GraphData = new WorldGraphData(),
                LogicData = new WorldLogicData(_worldSettings.GetWorldSize(), _worldSettings.ChunkSize),// 미리 2D 배열 할당

                PlacementDatas = new List<PlacementData>()
            };

            // 1~6단계: Story 생성 ~ Chunk 분리 까지 순차적으로 실행하는 파이프라인
            foreach (var stage in _pipeline)
            {
                stage.Initialize(_worldSettings);
                await stage.ExecuteAsync(_currentContext, ct);
                Debug.Log($"{stage.GetType().Name} 완료");
            }
            Debug.Log($"World Logic 생성 완료!");



        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("월드 생성이 취소되었습니다.");
            ClearWorld();
        }
    }
    #endregion
    


    private void ClearWorld()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (_currentContext != null)
        {
            _currentContext.Clear();
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
        if (!_drawGizmos || _currentContext == null || _currentContext.GraphData == null || _worldSettings == null) return;
        var graphData = _currentContext.GraphData;
        var logicData = _currentContext.LogicData;
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
                UnityEditor.Handles.Label(labelPos, $"{ node.RegionData.RegionName}: x:{nodePos.x} y:{nodePos.y} z:{nodePos.z}", style);
            }
#endif
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

                if(parentNode.RegionData != null && childNode.RegionData != null)
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
        var logicData = _currentContext.LogicData;
        var graphData = _currentContext.GraphData;
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
    public WorldLogicData GetWorldLogicData() => _currentContext.LogicData;
    public WorldGraphData GetWorldGraphData() => _currentContext.GraphData;   
    public List<PlacementData> GetWorldDisposeDatas() => _currentContext.PlacementDatas;
    #endregion

}



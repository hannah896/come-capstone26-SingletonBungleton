using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class FDNodeGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    [SerializeField] private MapSettings _mapSettings;
    
    [Header("Debug")]
    [SerializeField] private bool _enableStepByStep = false;
    [SerializeField] private float _stepDelay = 0.1f;

    // 캐싱된 설정값 (MapSettings에서 가져옴)
    private int _nodeCount;
    private int _maxDepth;
    private int _minChildNodes;
    private int _maxChildNodes;
    private int _simulationIterations;
    private float _repulsionStrength;
    private float _attractionStrength;
    private float _idealEdgeLength;
    private float _dampingFactor;
    private float _minNodeDistance;

    private ForceDirectedGraph _graph;
    private List<Vector2> _nodePositions = new();
    private CancellationTokenSource _cts;
    
    
    public List<Vector2> NodePositions => _nodePositions;
    public ForceDirectedGraph Graph => _graph;

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <summary>
    /// MapSettings에서 설정값 로드
    /// </summary>
    private void LoadSettingsFromMapSettings()
    {
        // MapSettings에서 값 로드
        _nodeCount = _mapSettings.NodeCount;
        _maxDepth = _mapSettings.MaxDepth;
        _minChildNodes = _mapSettings.MinChildNodes;
        _maxChildNodes = _mapSettings.MaxChildNodes;
        _simulationIterations = _mapSettings.SimulationIterations;
        _repulsionStrength = _mapSettings.RepulsionStrength;
        _attractionStrength = _mapSettings.AttractionStrength;
        _idealEdgeLength = _mapSettings.IdealEdgeLength;
        _dampingFactor = _mapSettings.DampingFactor;
        _minNodeDistance = _mapSettings.MinNodeDistance;
    }

    /// <summary>
    /// 맵 생성 (MapSettings 사용)
    /// </summary>
    public void GenerateNodeWithSettings(MapSettings settings)
    {
        _mapSettings = settings;
        GenerateNode();
    }

    /// <summary>
    /// 맵 생성 시작 (외부 호출용)
    /// </summary>
    public void GenerateNode()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        
        GenerateNodeAsync(_cts.Token).Forget();
    }



    /// <summary>
    /// 비동기 맵 생성 파이프라인
    /// </summary>
    public async UniTask GenerateNodeAsync(CancellationToken ct = default)
    {
        _nodePositions.Clear();
        
        // 설정값 로드
        LoadSettingsFromMapSettings();
        
        try
        {   // ===== Phase 1: Logical Topology =====
            Debug.Log("Phase 1: Logical Topology 생성 시작");
            GenerateLogicalTopology();
            Debug.Log($"Phase 1 완료: {_graph.Nodes.Count}개 노드, {_graph.Edges.Count}개 엣지");
            
            if (_enableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_stepDelay), cancellationToken: ct);
            
            // ===== Phase 2: Force Simulation =====
            Debug.Log("Phase 2: Force Simulation 시작");
            await RunForceSimulationAsync(ct);
            Debug.Log("Phase 2 완료: 좌표 확정");
            
            // ===== Phase 3: Result =====
            Debug.Log("Phase 3: 최종 좌표 추출");
            ExtractFinalPositions();
            Debug.Log($"Phase 3 완료: {_nodePositions.Count}개 좌표 생성");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성이 취소되었습니다.");
        }
    }

    #region Phase 1: Logical Topology
    /// <summary>
    /// Phase 1: 좌표 없이 노드와 엣지의 연결 관계만 생성
    /// </summary>
    private void GenerateLogicalTopology()
    {
        _graph = new ForceDirectedGraph();
        
        // Branch/Loop 설정 가져오기
        LandBranchSetting branchSetting = _mapSettings?.LandBranch ?? LandBranchSetting.Default;
        LandLoopSetting loopSetting = _mapSettings?.LandLoop ?? LandLoopSetting.Default;
        
        // 시작 노드 생성
        GraphNode startNode = _graph.CreateNode(0, true);
        
        // Branch 설정에 따른 자식 노드 수 범위 및 분기 확률
        int minChildren = _minChildNodes;
        int maxChildren = _maxChildNodes;
        float branchChance = 1.0f;  // 2개 이상 자식을 가질 확률
        int effectiveMaxDepth = _maxDepth;
        
        switch (branchSetting)
        {
            case LandBranchSetting.Never:
                // 대부분 1개, 가끔(20%) 2개 → 약간의 곁가지만 허용
                minChildren = 1;
                maxChildren = 2;
                branchChance = 0.2f;  // 20% 확률로만 2개 자식
                effectiveMaxDepth = Mathf.CeilToInt(_nodeCount * 0.8f);  // 긴 체인
                break;
            case LandBranchSetting.Least:
                minChildren = 1;
                maxChildren = 2;
                branchChance = 0.5f;  // 50% 확률로 2개 자식
                effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(_nodeCount) / Mathf.Log(1.5f)) + 2;
                break;
            case LandBranchSetting.Default:
                minChildren = 1;
                maxChildren = 3;
                branchChance = 1.0f; 
                effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(_nodeCount, 2)) + 2;
                break;
            case LandBranchSetting.Most:
                minChildren = 2;
                maxChildren = 5;
                branchChance = 1.0f;
                effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(_nodeCount, 3)) + 2;
                break;
        }
        
        // BFS 방식으로 그래프 확장
        Queue<(GraphNode node, int depth)> queue = new();
        queue.Enqueue((startNode, 0));
        
        int nodeId = 1;
        int targetNodeCount = _nodeCount;
        
        while (queue.Count > 0 && _graph.Nodes.Count < targetNodeCount)
        {
            var (currentNode, depth) = queue.Dequeue();
            
            // effectiveMaxDepth 사용
            if (depth >= effectiveMaxDepth) continue;

            // 확률 기반 자식 수 결정
            int childCount;
            if (Random.value > branchChance)
            {
                // 분기하지 않음 → 1개 자식만
                childCount = 1;
            }
            else
            {
                // 분기함 → minChildren ~ maxChildren 중 랜덤
                childCount = Random.Range(minChildren, maxChildren + 1);
            }
            
            // 남은 노드 수에 맞게 조정
            int remainingNodes = targetNodeCount - _graph.Nodes.Count;
            childCount = Mathf.Min(childCount, remainingNodes);
            
            for (int i = 0; i < childCount; i++)
            {
                GraphNode childNode = _graph.CreateNode(nodeId++);
                _graph.CreateEdge(currentNode, childNode);
                queue.Enqueue((childNode, depth + 1));
            }
        }
        
        // Loop 설정에 따라 순환 연결 추가
        ApplyLoopConnections(loopSetting);
    }

    /// <summary>
    /// Loop 설정에 따른 순환 연결 생성
    /// </summary>
    private void ApplyLoopConnections(LandLoopSetting loopSetting)
    {
        if (loopSetting == LandLoopSetting.Never) return;
        
        var nodes = _graph.Nodes;
        if (nodes.Count < 3) return;
        
        // 리프 노드들 찾기 (자식이 없는 노드)
        var leafNodes = nodes.Where(n => !n.IsStart && _graph.GetNeighbors(n).Count <= 1).ToList();
        
        switch (loopSetting)
        {
            case LandLoopSetting.Default:
                // 일부 리프 노드끼리 연결
                int loopCount = Mathf.Max(1, leafNodes.Count / 4);
                for (int i = 0; i < loopCount && leafNodes.Count >= 2; i++)
                {
                    int idx1 = Random.Range(0, leafNodes.Count);
                    GraphNode node1 = leafNodes[idx1];
                    leafNodes.RemoveAt(idx1);
                    
                    int idx2 = Random.Range(0, leafNodes.Count);
                    GraphNode node2 = leafNodes[idx2];
                    leafNodes.RemoveAt(idx2);
                    
                    if (!_graph.AreConnected(node1, node2))
                    {
                        _graph.CreateEdge(node1, node2);
                    }
                }
                break;
                
            case LandLoopSetting.Always:
                // 도넛 형태: 시작 노드와 가장 먼 리프 노드 연결 + 리프 노드끼리 순환
                if (leafNodes.Count >= 2)
                {
                    // 리프 노드들을 순환 연결
                    for (int i = 0; i < leafNodes.Count; i++)
                    {
                        GraphNode current = leafNodes[i];
                        GraphNode next = leafNodes[(i + 1) % leafNodes.Count];
                        
                        if (!_graph.AreConnected(current, next))
                        {
                            _graph.CreateEdge(current, next);
                        }
                    }
                }
                break;
        }
    }
    #endregion

    #region Phase 2: Force Simulation
    /// <summary>
    /// Phase 2: Force-Directed 알고리즘으로 좌표 결정
    /// </summary>
    private async UniTask RunForceSimulationAsync(CancellationToken ct)
    {
        // 초기 위치 설정 (원형 배치)
        InitializeNodePositions();
        
        for (int iteration = 0; iteration < _simulationIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            
            // 모든 노드의 힘 초기화
            foreach (var node in _graph.Nodes)
            {
                node.Force = Vector2.zero;
            }
            
            // 척력 계산 (모든 노드 쌍)
            CalculateRepulsionForces();
            
            // 인력 계산 (연결된 노드)
            CalculateAttractionForces();
            
            // 위치 업데이트
            UpdateNodePositions();
            
            // 시각적 디버깅을 위한 대기
            if (_enableStepByStep && iteration % 10 == 0)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(_stepDelay), cancellationToken: ct);
            }
        }
        
        // 최종 위치 정규화 (맵 크기에 맞게)
        NormalizePositions();
    }

    /// <summary>
    /// 노드 초기 위치 설정 (원형 배치)
    /// </summary>
    private void InitializeNodePositions()
    {
        var nodes = _graph.Nodes;
        int count = nodes.Count;
        
        // 시작 노드는 중앙에
        GraphNode startNode = nodes.FirstOrDefault(n => n.IsStart);
        if (startNode != null)
        {
            startNode.Position = Vector2.zero;
        }
        
        // 나머지 노드들은 깊이에 따라 원형으로 배치
        float baseRadius = _idealEdgeLength;
        
        foreach (var node in nodes.Where(n => !n.IsStart))
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = baseRadius * (node.Depth + 1) + Random.Range(-5f, 5f);
            
            node.Position = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }
    }

    /// <summary>
    /// 척력 계산 (쿨롱 법칙)
    /// 모든 노드는 서로를 밀어냄
    /// F = k * (q1 * q2) / r^2
    /// </summary>
    private void CalculateRepulsionForces()
    {
        var nodes = _graph.Nodes;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                GraphNode nodeA = nodes[i];
                GraphNode nodeB = nodes[j];
                
                Vector2 direction = nodeA.Position - nodeB.Position;
                float distance = direction.magnitude;
                
                // 최소 거리 보장 (0으로 나누기 방지)
                if (distance < 0.1f) distance = 0.1f;
                
                // 쿨롱 법칙: F = k / r^2
                // 거리의 제곱에 반비례하는 척력
                float forceMagnitude = _repulsionStrength / (distance * distance);
                
                Vector2 force = direction.normalized * forceMagnitude;
                
                // 뉴턴 제3법칙: 작용-반작용
                nodeA.Force += force;
                nodeB.Force -= force;
            }
        }
    }

    /// <summary>
    /// 인력 계산 (후크 법칙 / 스프링 법칙)
    /// 연결된 노드끼리는 서로를 당김
    /// F = -k * (x - x0)
    /// </summary>
    private void CalculateAttractionForces()
    {
        foreach (var edge in _graph.Edges)
        {
            GraphNode nodeA = edge.NodeA;
            GraphNode nodeB = edge.NodeB;
            
            Vector2 direction = nodeB.Position - nodeA.Position;
            float distance = direction.magnitude;
            
            // 후크 법칙: F = k * (현재 길이 - 이상적 길이)
            // 이상적 길이보다 멀면 당기고, 가까우면 밀어냄
            float displacement = distance - _idealEdgeLength;
            float forceMagnitude = _attractionStrength * displacement;
            
            Vector2 force = direction.normalized * forceMagnitude;
            
            // 양쪽 노드에 반대 방향으로 힘 적용
            nodeA.Force += force;
            nodeB.Force -= force;
        }
    }

    /// <summary>
    /// 노드 위치 업데이트 (오일러 적분)
    /// </summary>
    private void UpdateNodePositions()
    {
        foreach (var node in _graph.Nodes)
        {
            // 시작 노드는 중앙에 고정 (선택적)
            if (node.IsStart)
            {
                node.Position = Vector2.zero;
                continue;
            }
            
            // 속도 업데이트 (가속도 = 힘 / 질량, 질량 = 1로 가정)
            node.Velocity += node.Force;
            
            // 감쇠 적용 (에너지 손실)
            node.Velocity *= _dampingFactor;
            
            // 위치 업데이트
            node.Position += node.Velocity;
        }
        
        // 노드 간 최소 거리 보장
        EnforceMinimumDistance();
    }

    /// <summary>
    /// 노드 간 최소 거리 보장
    /// </summary>
    private void EnforceMinimumDistance()
    {
        var nodes = _graph.Nodes;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                GraphNode nodeA = nodes[i];
                GraphNode nodeB = nodes[j];
                
                Vector2 direction = nodeA.Position - nodeB.Position;
                float distance = direction.magnitude;
                
                if (distance < _minNodeDistance && distance > 0.01f)
                {
                    // 최소 거리까지 밀어냄
                    float overlap = _minNodeDistance - distance;
                    Vector2 pushDirection = direction.normalized * (overlap / 2f);
                    
                    if (!nodeA.IsStart) nodeA.Position += pushDirection;
                    if (!nodeB.IsStart) nodeB.Position -= pushDirection;
                }
            }
        }
    }

    /// <summary>
    /// 최종 위치를 맵 크기에 맞게 정규화
    /// </summary>
    private void NormalizePositions()
    {
        if (_graph.Nodes.Count == 0) return;
        
        Vector2Int mapSize = _mapSettings?.GetMapSize() ?? new Vector2Int(120, 120);
        
        // 현재 바운딩 박스 계산
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        
        foreach (var node in _graph.Nodes)
        {
            minX = Mathf.Min(minX, node.Position.x);
            maxX = Mathf.Max(maxX, node.Position.x);
            minY = Mathf.Min(minY, node.Position.y);
            maxY = Mathf.Max(maxY, node.Position.y);
        }
        
        float currentWidth = maxX - minX;
        float currentHeight = maxY - minY;
        
        // 여백을 둔 목표 크기
        float targetWidth = mapSize.x * 0.8f;
        float targetHeight = mapSize.y * 0.8f;
        float offsetX = mapSize.x * 0.1f;
        float offsetY = mapSize.y * 0.1f;
        
        // 스케일 계산 (비율 유지)
        float scale = Mathf.Min(
            currentWidth > 0 ? targetWidth / currentWidth : 1f,
            currentHeight > 0 ? targetHeight / currentHeight : 1f
        );
        
        // 정규화 적용
        foreach (var node in _graph.Nodes)
        {
            node.Position = new Vector2(
                ((node.Position.x - minX) * scale) + offsetX,
                ((node.Position.y - minY) * scale) + offsetY
            );
        }
    }
    #endregion

    #region Phase 3: Result
    /// <summary>
    /// Phase 3: 최종 좌표 추출
    /// </summary>
    private void ExtractFinalPositions()
    {
        _nodePositions.Clear();
        
        foreach (var node in _graph.Nodes)
        {
            _nodePositions.Add(node.Position);
        }
    }

    /// <summary>
    /// 노드 좌표 리스트 반환 (MapGenerator 연동용)
    /// </summary>
    public List<(Vector2, int)> GetNodePositions()
    {
        var data = new List<(Vector2, int)>();
        foreach (var node in _graph.Nodes)
        {
            data.Add((node.Position, node.Depth));
        }
        return data;
    }

    /// <summary>
    /// 엣지 정보를 (노드ID, 노드ID) 튜플 리스트로 반환
    /// </summary>
    public List<(int, int)> GetConnections()
    {
        return _graph.Edges
            .Select(e => (e.NodeA.Id, e.NodeB.Id))
            .ToList();
    }
    #endregion
}

#region Graph Data Classes
/// <summary>
/// 그래프 노드
/// </summary>
public class GraphNode
{
    public int Id { get; set; }
    public int Depth { get; set; }
    public bool IsStart { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Force { get; set; }
    
    public GraphNode(int id, bool isStart = false)
    {
        Id = id;
        IsStart = isStart;
        Depth = isStart ? 0 : -1;
        Position = Vector2.zero;
        Velocity = Vector2.zero;
        Force = Vector2.zero;
    }
}

/// <summary>
/// 그래프 엣지
/// </summary>
public class GraphEdge
{
    public GraphNode NodeA { get; }
    public GraphNode NodeB { get; }
    
    public GraphEdge(GraphNode a, GraphNode b)
    {
        NodeA = a;
        NodeB = b;
    }
}



/// <summary>
/// Force-Directed Graph 관리 클래스
/// </summary>
public class ForceDirectedGraph
{
    private List<GraphNode> _nodes = new();
    private List<GraphEdge> _edges = new();
    private Dictionary<GraphNode, List<GraphNode>> _adjacencyList = new();
    
    public List<GraphNode> Nodes => _nodes;
    public List<GraphEdge> Edges => _edges;
    
    /// <summary>
    /// 노드 생성
    /// </summary>
    public GraphNode CreateNode(int id, bool isStart = false)
    {
        GraphNode node = new GraphNode(id, isStart);
        _nodes.Add(node);
        _adjacencyList[node] = new List<GraphNode>();
        return node;
    }
    
    /// <summary>
    /// 엣지 생성 (양방향)
    /// </summary>
    public GraphEdge CreateEdge(GraphNode nodeA, GraphNode nodeB)
    {
        if (AreConnected(nodeA, nodeB)) return null;
        
        GraphEdge edge = new GraphEdge(nodeA, nodeB);
        _edges.Add(edge);
        
        _adjacencyList[nodeA].Add(nodeB);
        _adjacencyList[nodeB].Add(nodeA);
        
        // 깊이 업데이트
        if (nodeA.Depth >= 0 && nodeB.Depth < 0)
        {
            nodeB.Depth = nodeA.Depth + 1;
        }
        else if (nodeB.Depth >= 0 && nodeA.Depth < 0)
        {
            nodeA.Depth = nodeB.Depth + 1;
        }
        
        return edge;
    }
    
    /// <summary>
    /// 두 노드가 연결되어 있는지 확인
    /// </summary>
    public bool AreConnected(GraphNode nodeA, GraphNode nodeB)
    {
        return _adjacencyList.ContainsKey(nodeA) && _adjacencyList[nodeA].Contains(nodeB);
    }
    
    /// <summary>
    /// 노드의 이웃 노드들 반환
    /// </summary>
    public List<GraphNode> GetNeighbors(GraphNode node)
    {
        return _adjacencyList.ContainsKey(node) ? _adjacencyList[node] : new List<GraphNode>();
    }
}
#endregion

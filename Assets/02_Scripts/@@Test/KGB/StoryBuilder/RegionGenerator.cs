using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// 단일 Task의 Room 그래프를 생성하는 클래스
/// </summary>
public class RegionGenerator
{
    #region Settings
    [System.Serializable]
    public class ForceSimSettings
    {
        [Header("Force Simulation")]
        public int simulationIterations = 150;
        public float repulsionStrength = 250f;
        public float attractionStrength = 0.3f;
        public float idealEdgeLength = 15f;
        public float dampingFactor = 0.9f;
        public float minNodeDistance = 10f;
        
        [Header("Debug")]
        public bool enableStepByStep = false;
        public float stepDelay = 0.1f;
    }
    #endregion

    private ForceSimSettings _settings;
    private ForceDirectedRoomGraph _forcedGraph;

    private Dictionary<Node, List<RoomGraphNode>> _regionToRoomsMap = new();


    /// <summary>
    /// RegionData 기반으로 Room 그래프 생성
    /// </summary>
    public async UniTask<GraphResult> ConvertRegionsToRoomsAsync(
        List<Node> regionNodes,  
        List<NodeConnection> regionConnections, 
        CancellationToken ct)
    {
        _forcedGraph = new ForceDirectedRoomGraph();
        _regionToRoomsMap.Clear();

        try
        {
            foreach (var regionNode in regionNodes)
                GenerateInternalTopology(regionNode);
            
            if (_settings.enableStepByStep)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_settings.stepDelay), cancellationToken: ct);

            // 3: 전체 Room들에 대한 Force Simulation 실행
            await RunForceSimulationAsync(ct);

            // 4: 오프셋 적용 및 결과 생성
            return CreateResultNodes();
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log($"Task '{taskData.taskName}' 생성이 취소되었습니다.");
            return new GraphResult();
        }
    }

    #region Phase 1: Room Topology
    private void GenerateInternalTopology(Node regionNode)
    {
        RegionData regionData = regionNode.RegionData;
        int roomCount = regionData.RoomCount;
        float branchFactor = regionData.RoomBranch;

        List<RoomGraphNode> clusterRooms = new List<RoomGraphNode>();

        RoomGraphNode startRoom = _forcedGraph.CreateNode(0, isStart: true);
        // branchFactor에 따른 분기 설정
        int minChildren, maxChildren;
        float branchChance;
        int effectiveMaxDepth;
        
        if (branchFactor < 0.25f)
        {
            // 뱀 형태 (거의 일직선)
            minChildren = 1;
            maxChildren = 2;
            branchChance = 0.1f + branchFactor;
            effectiveMaxDepth = Mathf.CeilToInt(roomCount * 0.9f);
        }
        else if (branchFactor < 0.5f)
        {
            // 약간의 분기
            minChildren = 1;
            maxChildren = 2;
            branchChance = 0.3f + branchFactor;
            effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(roomCount) / Mathf.Log(1.5f)) + 2;
        }
        else if (branchFactor < 0.75f)
        {
            // 중간 분기
            minChildren = 1;
            maxChildren = 3;
            branchChance = 0.7f + branchFactor * 0.3f;
            effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(roomCount, 2)) + 2;
        }
        else
        {
            // 성게 형태 (많은 분기)
            minChildren = 2;
            maxChildren = 4;
            branchChance = 1.0f;
            effectiveMaxDepth = Mathf.CeilToInt(Mathf.Log(roomCount, 3)) + 1;
        }
        
        // BFS 방식으로 그래프 확장
        Queue<(RoomGraphNode node, int depth)> queue = new();
        queue.Enqueue((startNode, 0));
        
        int nodeId = 1;
        
        while (queue.Count > 0 && _graph.Nodes.Count < roomCount)
        {
            var (currentNode, depth) = queue.Dequeue();
            
            if (depth >= effectiveMaxDepth) continue;

            int childCount;
            if (Random.value > branchChance)
                childCount = 1;
            else
                childCount = Random.Range(minChildren, maxChildren + 1);
            
            int remainingNodes = roomCount - _graph.Nodes.Count;
            childCount = Mathf.Min(childCount, remainingNodes);
            
            for (int i = 0; i < childCount; i++)
            {
                RoomGraphNode childNode = _graph.CreateNode(nodeId++);
                _graph.CreateEdge(currentNode, childNode);
                queue.Enqueue((childNode, depth + 1));
            }
        }
    }
    #endregion

    #region Phase 2: Force Simulation
    private async UniTask RunForceSimulationAsync(CancellationToken ct)
    {
        InitializeNodePositions();
        
        for (int iteration = 0; iteration < _settings.simulationIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();
            
            foreach (var node in _graph.Nodes)
                node.Force = Vector2.zero;
            
            CalculateRepulsionForces();
            CalculateAttractionForces();
            UpdateNodePositions();
            
            if (_settings.enableStepByStep && iteration % 10 == 0)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_settings.stepDelay), cancellationToken: ct);
        }
    }

    private void InitializeNodePositions()
    {
        RoomGraphNode startNode = _graph.Nodes.FirstOrDefault(n => n.IsStart);
        if (startNode != null)
            startNode.Position = Vector2.zero;
        
        foreach (var node in _graph.Nodes.Where(n => !n.IsStart))
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = _settings.idealEdgeLength * (node.Depth + 1) + Random.Range(-5f, 5f);
            node.Position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }

    private void CalculateRepulsionForces()
    {
        var nodes = _graph.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distance = Mathf.Max(0.1f, direction.magnitude);
                float forceMagnitude = _settings.repulsionStrength / (distance * distance);
                Vector2 force = direction.normalized * forceMagnitude;
                
                nodes[i].Force += force;
                nodes[j].Force -= force;
            }
        }
    }

    private void CalculateAttractionForces()
    {
        foreach (var edge in _graph.Edges)
        {
            Vector2 direction = edge.NodeB.Position - edge.NodeA.Position;
            float displacement = direction.magnitude - _settings.idealEdgeLength;
            Vector2 force = direction.normalized * (_settings.attractionStrength * displacement);
            
            edge.NodeA.Force += force;
            edge.NodeB.Force -= force;
        }
    }

    private void UpdateNodePositions()
    {
        foreach (var node in _graph.Nodes)
        {
            if (node.IsStart)
            {
                node.Position = Vector2.zero;
                continue;
            }
            
            node.Velocity = (node.Velocity + node.Force) * _settings.dampingFactor;
            node.Position += node.Velocity;
        }
        
        // 최소 거리 보장
        var nodes = _graph.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                Vector2 direction = nodes[i].Position - nodes[j].Position;
                float distance = direction.magnitude;
                
                if (distance < _settings.minNodeDistance && distance > 0.01f)
                {
                    float overlap = _settings.minNodeDistance - distance;
                    Vector2 push = direction.normalized * (overlap / 2f);
                    
                    if (!nodes[i].IsStart) nodes[i].Position += push;
                    if (!nodes[j].IsStart) nodes[j].Position -= push;
                }
            }
        }
    }
    #endregion

    #region Phase 3: Result
    private void ApplyOffset(Vector2 offset)
    {
        foreach (var node in _graph.Nodes)
            node.Position += offset;
    }

    private List<TaskRoom> CreateTaskRooms(RegionData regionData)
    {
        var rooms = new List<TaskRoom>();
        
        foreach (var node in _graph.Nodes)
        {
            rooms.Add(new TaskRoom
            {
                id = node.Id,
                position = node.Position,
                depth = node.Depth,
                isEntrance = node.IsStart,
                roomData = GetRoomDataForNode(taskData, node),
                taskData = taskData
            });
        }
        
        return rooms;
    }

    private RoomData GetRoomDataForNode(TaskData taskData, RoomGraphNode node)
    {
        if (node.IsStart && taskData.entranceRoom != null)
            return taskData.entranceRoom;
        
        if (node.Depth == taskData.roomCount - 1 && taskData.specialRoom != null)
            return taskData.specialRoom;
        
        if (taskData.defaultRooms != null && taskData.defaultRooms.Count > 0)
            return taskData.defaultRooms[Random.Range(0, taskData.defaultRooms.Count)];
        
        return null;
    }
    #endregion
}

#region Room Graph Classes
/// <summary>
/// Room 그래프 노드
/// </summary>
public class RoomGraphNode
{
    public Node RegionNode; // 중요: 내가 속한 Task(Region)가 누군지
    public bool IsEntrance; // 내가 이 구역의 입구인지
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Force;
    
    public RoomGraphNode()
    { 
        Position = Vector2.zero;
        Velocity = Vector2.zero;
        Force = Vector2.zero;
    }
}

/// <summary>
/// Room Force-Directed Graph 관리 클래스
/// </summary>
public class ForceDirectedRoomGraph
{
    private List<Node> _nodes = new();
    private List<NodeConnection> _connections = new();
    private Dictionary<RoomGraphNode, List<RoomGraphNode>> _adjacencyList = new();
    
    public RoomGraphNode CreateNode(int id, bool isStart = false)
    {
        RoomGraphNode node = new RoomGraphNode(id, isStart);
        _nodes.Add(node);
        _adjacencyList[node] = new List<RoomGraphNode>();
        return node;
    }
    
    public RoomGraphEdge CreateEdge(RoomGraphNode nodeA, RoomGraphNode nodeB)
    {
        if (AreConnected(nodeA, nodeB)) return null;

        RoomGraphEdge edge = new RoomGraphEdge(nodeA, nodeB);
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
    
    public bool AreConnected(RoomGraphNode nodeA, RoomGraphNode nodeB)
    {
        return _adjacencyList.ContainsKey(nodeA) && _adjacencyList[nodeA].Contains(nodeB);
    }
}


#endregion
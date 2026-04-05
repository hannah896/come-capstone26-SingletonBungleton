using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ForceSimulator : IGraphPipelineStage
{
    private const float DEFAULT_PADDING = 0.1f;
    private const float MIN_DIMENSION_SIZE = 0.1f;

    private WorldSettings _worldSettings;
    private System.Random _prng;
    private WorldGraphData _simulationResult;
    private CancellationToken _ct;
    
    private ForceSimParameters _simParameter;

    [System.Serializable]
    public class ForceSimParameters
    {
        public int simulationIterations;
        public float repulsionStrength;
        public float attractionStrength;
        public float idealEdgeLength;
        public float dampingFactor;
        public float minNodeDistance;
    }

    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _prng = new System.Random(settings.WorldSeed + (int)WorldSeedChannel.ForceSimulator);
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _simulationResult = ctx.GraphData;
        _ct = ct;

        // 0. 시뮬레이션 환경 초기 세팅
        await SetupSimulationParameters();

        // 1. 방사형 초기 배치 (꼬임 방지)
        await ArrangeNodesInitial();

        // 2. 물리 시뮬레이션
        await RunForceSimulationAsync();

        // 3. 루프 절단
        await CutLoopConnections();

        // 4. 맵 크기에 맞춰 스케일 및 중앙 정렬
        await FitNodesToTileGridAsync();
    }

    /// <summary>
    /// 시뮬레이션 파라미터를 동적으로 구성하는 초기화 함수
    /// </summary>
    private async UniTask SetupSimulationParameters()
    {
        Vector2Int mapSize = _worldSettings.GetWorldSize();
        int nodeCount = _simulationResult.Nodes.Count; // 이제 이 nodeCount는 항상 Region의 갯수입니다.
        float mapArea = mapSize.x * mapSize.y;
        float regionUnit = Mathf.Sqrt(mapArea / Mathf.Max(1, nodeCount));
        
        ForceSimulatorSettings simSettings = _worldSettings.ForceSimulatorSettings;
        
        // 단일 시뮬레이션 파라미터 생성
        _simParameter = CreateDynamicSettings(
            unit: regionUnit,      
            lengthRatio: simSettings.macroLengthRatio,         
            repulsionFactor: simSettings.macroRepulsionFactor,      
            iterations: simSettings.maxIterations 
        );
        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private async UniTask ArrangeNodesInitial()
    {
        int regionCount = _simulationResult.Nodes.Count;

        if (!_simulationResult.IsLooped)
        {
            // 트리 형태로 앞으로만 뻗어 나가게 유도
            foreach (var node in _simulationResult.Nodes)
            {
                if (node.Depth == 1) 
                    node.Position = GetInsideUnitCircle() * _simParameter.idealEdgeLength * 0.5f;
                else
                {
                    Node parentNode = _simulationResult.GetParentNode(node);
                    if (parentNode != null)
                    {
                        // 좁은 각도로 배치하여 초기부터 가지가 꼬이지 않게 방지
                        var dir = GetForwardDirection(node, parentNode);
                        node.Position = parentNode.Position + (dir * _simParameter.idealEdgeLength);
                    }
                }
            }
        }
        else
        {
            // 루프맵일 경우 원형 도넛 배치
            float circumference = regionCount * _simParameter.idealEdgeLength;
            float radius = Mathf.Max(circumference / (2 * Mathf.PI), _simParameter.idealEdgeLength * 1.5f);

            var sortedNodes = _simulationResult.Nodes.OrderBy(n => n.Depth).ToList();
            for (int i = 0; i < regionCount; i++)
            {
                float angle = i * (360f / regionCount) * Mathf.Deg2Rad;
                sortedNodes[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private async UniTask RunForceSimulationAsync()
    {
        List<Node> nodes = _simulationResult.Nodes;

        for (int iteration = 0; iteration < _simParameter.simulationIterations; iteration++)
        {
            _ct.ThrowIfCancellationRequested();

            foreach (var node in nodes) node.Force = Vector2.zero;

            // 척력 (밀어내기)
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Vector2 dir = nodes[i].Position - nodes[j].Position;
                    float distSqr = Mathf.Max(dir.sqrMagnitude, 0.01f);

                    // TerritoryScale을 사용하여 거리 조절
                    float weightI = (nodes[i].RegionData != null) ? Mathf.Sqrt(nodes[i].RegionData.TerritoryScale) : 1f;
                    float weightJ = (nodes[j].RegionData != null) ? Mathf.Sqrt(nodes[j].RegionData.TerritoryScale) : 1f;

                    float forceMag = (_simParameter.repulsionStrength * weightI * weightJ) / distSqr;
                    Vector2 force = dir.normalized * forceMag;

                    nodes[i].Force += force;
                    nodes[j].Force -= force;
                }
            }

            // 인력 (스프링)
            foreach (var edge in _simulationResult.NodeConnections)
            {
                Vector2 dir = edge.ChildNode.Position - edge.ParentNode.Position;
                float displacement = dir.magnitude - _simParameter.idealEdgeLength;
                Vector2 force = dir.normalized * (_simParameter.attractionStrength * displacement);

                edge.ParentNode.Force += force;
                edge.ChildNode.Force -= force;
            }

            // 위치 갱신
            foreach (var node in nodes)
            {
                float weight = (node.RegionData != null) ? Mathf.Sqrt(Mathf.Max(1, node.RegionData.TerritoryScale)) : 1f;
                // 속도 제한을 걸어 통과(Ghosting) 방지
                float maxVelocity = _simParameter.idealEdgeLength * weight * 0.15f; 

                if (node.Force.magnitude > maxVelocity)
                    node.Force = node.Force.normalized * maxVelocity;

                node.Velocity = (node.Velocity + node.Force) * _simParameter.dampingFactor;

                if (node.Velocity.magnitude > maxVelocity)
                    node.Velocity = node.Velocity.normalized * maxVelocity;
                    
                node.Position += node.Velocity;
            }

            if (_worldSettings.EnableStepByStep && iteration % 10 == 0)
                await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
        }
    }

    private async UniTask CutLoopConnections()
    {
        int removedCount = _simulationResult.NodeConnections.RemoveAll(conn => conn.ParentNode.Depth > conn.ChildNode.Depth);
        _simulationResult.RebuildAdjacency();

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private async UniTask FitNodesToTileGridAsync()
    {
        var nodes = _simulationResult.Nodes;
        if (nodes.Count == 0) return;

        float minX = nodes.Min(n => n.Position.x), maxX = nodes.Max(n => n.Position.x);
        float minY = nodes.Min(n => n.Position.y), maxY = nodes.Max(n => n.Position.y);

        float currentWidth = Mathf.Max(maxX - minX, MIN_DIMENSION_SIZE);
        float currentHeight = Mathf.Max(maxY - minY, MIN_DIMENSION_SIZE);

        float targetWidth = _worldSettings.GetWorldSize().x * (1f - DEFAULT_PADDING * 2);
        float targetHeight = _worldSettings.GetWorldSize().y * (1f - DEFAULT_PADDING * 2);

        float finalScale = Mathf.Min(targetWidth / currentWidth, targetHeight / currentHeight);
        Vector2 currentCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 targetCenter = new Vector2(_worldSettings.GetWorldSize().x * 0.5f, _worldSettings.GetWorldSize().y * 0.5f);

        foreach (var node in nodes)
        {
            node.Position = targetCenter + ((node.Position - currentCenter) * finalScale);
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private Vector2 GetInsideUnitCircle()
    {
        float angle = (float)_prng.NextDouble() * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Sqrt((float)_prng.NextDouble());
    }

    private Vector2 GetForwardDirection(Node targetNode, Node parentNode)
    {
        var parentConn = _simulationResult.NodeConnections.FirstOrDefault(c => c.ChildNode == parentNode);
        Vector2 baseDir = (parentConn != null) ? (parentNode.Position - parentConn.ParentNode.Position).normalized : Vector2.up;
        if (baseDir == Vector2.zero) baseDir = Vector2.up;

        // 역주행 및 꼬임 현상 원천 차단 (-45도 ~ 45도 안에서만 자라남)
        float randomAngle = _prng.Next(-45, 46);
        return (Quaternion.Euler(0, 0, randomAngle) * baseDir).normalized;
    }

    private ForceSimParameters CreateDynamicSettings(float unit, float lengthRatio, float repulsionFactor, int iterations)
    {
        float targetEdgeLength = unit * lengthRatio;
        return new ForceSimParameters
        {
            simulationIterations = iterations,
            dampingFactor = 0.82f,
            idealEdgeLength = targetEdgeLength,
            minNodeDistance = targetEdgeLength * 0.4f,
            repulsionStrength = (targetEdgeLength * targetEdgeLength) * repulsionFactor,
            attractionStrength = 0.2f
        };
    }
}

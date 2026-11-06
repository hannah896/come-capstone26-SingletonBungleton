using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ForceSimulator : IGraphPipelineStage
{
    private const float DEFAULT_PADDING = 0.1f;
    private const float MIN_DIMENSION_SIZE = 0.1f;
    private const float GEOMETRY_EPSILON = 0.0001f;

    private WorldSettings _worldSettings;
    private System.Random _prng;
    private WorldGraphData _simulationResult;
    private CancellationToken _ct;
    private ForceSimParameters _simParameter;
    private float _minimumClearance;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;
    private readonly Dictionary<Node, List<Node>> _children = new();
    private readonly Dictionary<Node, int> _leafCounts = new();

    [Serializable]
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
        ct.ThrowIfCancellationRequested();
        _simulationResult = ctx.GraphData;
        _ct = ct;
        await SetupSimulationParameters();

        // 최종 결과에서 제거하던 루프 닫기 간선을 먼저 제거한다.
        // 남을 트리와 관계없는 인력이 초기 배치를 꼬이게 하지 않도록 한다.
        await CutLoopConnections();
        await ArrangeNodesInitial();
        await RunForceSimulationAsync();

        // 처음부터 타일 좌표에서 간격을 검사하므로 마지막 재축소로 육로 폭이 줄지 않는다.
        if (MeasureLayoutClearance() + GEOMETRY_EPSILON < _minimumClearance)
            throw new InvalidOperationException("지역 배치가 필수 육로의 안전 간격을 유지하지 못했습니다.");
    }

    private async UniTask SetupSimulationParameters()
    {
        Vector2Int mapSize = _worldSettings.GetWorldSize();
        float mapArea = (float)mapSize.x * mapSize.y;
        float regionUnit = Mathf.Sqrt(mapArea / Mathf.Max(1, _simulationResult.Nodes.Count));
        ForceSimulatorSettings settings = _worldSettings.ForceSimulatorSettings;
        _simParameter = CreateDynamicSettings(regionUnit, settings.macroLengthRatio,
            settings.macroRepulsionFactor, settings.maxIterations);

        // 양쪽 육로의 반경과 래스터화 여유, 이웃 타일의 잘못된 접촉까지 피하는 간격이다.
        _minimumClearance = Mathf.Max(2, _worldSettings.PartitionSettings.minimumConnectionWidth) + 4f;
        _boundsMin = new Vector2(Mathf.Max(mapSize.x * DEFAULT_PADDING, _minimumClearance),
            Mathf.Max(mapSize.y * DEFAULT_PADDING, _minimumClearance));
        _boundsMax = new Vector2(mapSize.x - 1f - _boundsMin.x, mapSize.y - 1f - _boundsMin.y);
        if (_boundsMax.x <= _boundsMin.x || _boundsMax.y <= _boundsMin.y)
            throw new InvalidOperationException("필수 육로 폭에 비해 월드 크기가 너무 작습니다.");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private async UniTask ArrangeNodesInitial()
    {
        List<Node> nodes = _simulationResult.Nodes;
        if (nodes.Count == 0) return;
        _children.Clear();
        _leafCounts.Clear();
        var parentCounts = nodes.ToDictionary(node => node, _ => 0);
        foreach (Node node in nodes)
        {
            _children.Add(node, new List<Node>());
            node.Velocity = node.Force = Vector2.zero;
        }
        foreach (NodeConnection edge in _simulationResult.NodeConnections)
        {
            if (!_children.ContainsKey(edge.ParentNode) || !_children.ContainsKey(edge.ChildNode) ||
                ++parentCounts[edge.ChildNode] > 1)
                throw new InvalidOperationException("지역 초기 배치는 부모가 하나인 트리 연결이 필요합니다.");
            _children[edge.ParentNode].Add(edge.ChildNode);
        }
        // 순회 순서는 고정하고 순서 선택에만 시드 난수를 사용한다.
        foreach (Node node in nodes)
        {
            List<Node> children = _children[node];
            for (int i = children.Count - 1; i > 0; i--)
            {
                int j = _prng.Next(i + 1);
                (children[i], children[j]) = (children[j], children[i]);
            }
        }
        List<Node> roots = nodes.Where(node => parentCounts[node] == 0).ToList();
        foreach (Node root in roots) CountLeaves(root);
        if (_leafCounts.Count != nodes.Count)
            throw new InvalidOperationException("지역 연결에 순환이 남아 있어 분기 배치를 만들 수 없습니다.");

        float rotation = (float)_prng.NextDouble() * Mathf.PI * 2f;
        float cursor = rotation;
        int totalLeaves = roots.Sum(root => _leafCounts[root]);
        foreach (Node root in roots)
        {
            float span = Mathf.PI * 2f * _leafCounts[root] / totalLeaves;
            ArrangeSector(root, roots.Count == 1 ? 0 : 1, cursor, span);
            cursor += span;
        }
        FitNodesToTileGrid();

        // 깊고 좁은 분기는 방사형 배치에서 필요한 폭을 확보하지 못할 수 있다.
        // 같은 자식 순서를 유지하는 층별 트리 배치를 여러 종횡비로 검사한다.
        float bestClearance = MeasureLayoutClearance();
        Vector2[] bestPositions = nodes.Select(node => node.Position).ToArray();
        if (bestClearance + GEOMETRY_EPSILON < _minimumClearance)
        {
            foreach (float aspect in new[] { 0.25f, 0.5f, 1f, 2f, 4f, 8f })
            {
                float nextLeaf = 0f;
                foreach (Node root in roots)
                {
                    ArrangeLayered(root, 0, aspect, ref nextLeaf);
                    nextLeaf += 1f;
                }
                FitNodesToTileGrid();
                float clearance = MeasureLayoutClearance();
                if (clearance <= bestClearance) continue;
                bestClearance = clearance;
                for (int i = 0; i < nodes.Count; i++) bestPositions[i] = nodes[i].Position;
            }
            for (int i = 0; i < nodes.Count; i++) nodes[i].Position = bestPositions[i];
        }
        if (bestClearance + GEOMETRY_EPSILON < _minimumClearance)
            throw new InvalidOperationException("지역 수와 필수 육로 폭에 맞는 교차 없는 배치를 만들 수 없습니다.");

        // 좌표를 맞춘 뒤의 길이를 사용해 힘의 크기도 타일 단위로 맞춘다.
        if (_simulationResult.NodeConnections.Count > 0)
        {
            float fittedLength = _simulationResult.NodeConnections.Average(edge =>
                Vector2.Distance(edge.ParentNode.Position, edge.ChildNode.Position));
            float scale = fittedLength / Mathf.Max(MIN_DIMENSION_SIZE, _simParameter.idealEdgeLength);
            _simParameter.idealEdgeLength = fittedLength;
            _simParameter.repulsionStrength *= scale * scale;
            _simParameter.minNodeDistance = _minimumClearance;
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private int CountLeaves(Node node)
    {
        int leaves = _children[node].Count == 0 ? 1 : _children[node].Sum(CountLeaves);
        _leafCounts.Add(node, leaves);
        return leaves;
    }

    private void ArrangeSector(Node node, int depth, float start, float span)
    {
        float angle = start + span * 0.5f;
        node.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (depth * _simParameter.idealEdgeLength);
        if (_children[node].Count == 0) return;

        // 자손은 자기 분기의 각도 안에서만 자란다. 루트 외에는 뒤쪽으로 접히지 않게 한다.
        float childSpan = depth == 0 ? span : Mathf.Min(span, Mathf.PI * 0.85f);
        float cursor = angle - childSpan * 0.5f;
        foreach (Node child in _children[node])
        {
            float portion = childSpan * _leafCounts[child] / _leafCounts[node];
            ArrangeSector(child, depth + 1, cursor, portion);
            cursor += portion;
        }
    }

    private float ArrangeLayered(Node node, int depth, float aspect, ref float nextLeaf)
    {
        float x;
        if (_children[node].Count == 0) x = nextLeaf++;
        else
        {
            float first = 0f, last = 0f;
            for (int i = 0; i < _children[node].Count; i++)
            {
                last = ArrangeLayered(_children[node][i], depth + 1, aspect, ref nextLeaf);
                if (i == 0) first = last;
            }
            x = (first + last) * 0.5f;
        }
        node.Position = new Vector2(x, depth * aspect) * _simParameter.idealEdgeLength;
        return x;
    }

    private async UniTask RunForceSimulationAsync()
    {
        List<Node> nodes = _simulationResult.Nodes;
        for (int iteration = 0; iteration < _simParameter.simulationIterations; iteration++)
        {
            _ct.ThrowIfCancellationRequested();
            foreach (Node node in nodes) node.Force = Vector2.zero;

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Vector2 dir = nodes[i].Position - nodes[j].Position;
                    float distSqr = Mathf.Max(dir.sqrMagnitude, 0.01f);
                    float weightI = Mathf.Sqrt(Mathf.Max(1f, nodes[i].RegionData != null ? nodes[i].RegionData.TerritoryScale : 1f));
                    float weightJ = Mathf.Sqrt(Mathf.Max(1f, nodes[j].RegionData != null ? nodes[j].RegionData.TerritoryScale : 1f));
                    Vector2 force = dir.normalized * (_simParameter.repulsionStrength * weightI * weightJ / distSqr);
                    nodes[i].Force += force;
                    nodes[j].Force -= force;
                }
            }
            foreach (NodeConnection edge in _simulationResult.NodeConnections)
            {
                Vector2 dir = edge.ChildNode.Position - edge.ParentNode.Position;
                Vector2 force = dir.normalized * (_simParameter.attractionStrength * (dir.magnitude - _simParameter.idealEdgeLength));
                edge.ParentNode.Force += force;
                edge.ChildNode.Force -= force;
            }
            foreach (Node node in nodes)
            {
                float weight = Mathf.Sqrt(Mathf.Max(1f, node.RegionData != null ? node.RegionData.TerritoryScale : 1f));
                float maxVelocity = _simParameter.idealEdgeLength * weight * 0.15f;
                node.Force = Vector2.ClampMagnitude(node.Force, maxVelocity);
                node.Velocity = Vector2.ClampMagnitude((node.Velocity + node.Force) * _simParameter.dampingFactor, maxVelocity);
                MoveNodeSafely(node);
            }
            if (_worldSettings.EnableStepByStep && iteration % 10 == 0)
                await UniTask.Delay(TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
        }
    }

    private void MoveNodeSafely(Node node)
    {
        Vector2 origin = node.Position;
        int steps = Mathf.Max(1, Mathf.CeilToInt(node.Velocity.magnitude / (_minimumClearance * 0.25f)));
        Vector2 step = node.Velocity / steps;
        for (int i = 0; i < steps; i++)
        {
            bool accepted = false;
            Vector2 attemptedStep = step;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 target = node.Position + attemptedStep;
                target.x = Mathf.Clamp(target.x, _boundsMin.x, _boundsMax.x);
                target.y = Mathf.Clamp(target.y, _boundsMin.y, _boundsMax.y);
                if (CanMoveNode(node, target))
                {
                    node.Position = target;
                    accepted = true;
                    break;
                }
                attemptedStep *= 0.5f;
            }
            if (!accepted || attemptedStep != step) break;
        }
        node.Velocity = node.Position - origin;
    }

    private bool CanMoveNode(Node node, Vector2 target)
    {
        Vector2 origin = node.Position;
        float minSqr = _minimumClearance * _minimumClearance - GEOMETRY_EPSILON;
        foreach (Node other in _simulationResult.Nodes)
            if (other != node && PointSegmentDistanceSqr(other.Position, origin, target) < minSqr) return false;

        foreach (NodeConnection fixedEdge in _simulationResult.NodeConnections)
        {
            if (fixedEdge.ParentNode == node || fixedEdge.ChildNode == node) continue;
            if (SegmentDistanceSqr(origin, target, fixedEdge.ParentNode.Position, fixedEdge.ChildNode.Position) < minSqr)
                return false;
        }
        foreach (NodeConnection movingEdge in _simulationResult.NodeConnections)
        {
            Node anchor;
            if (movingEdge.ParentNode == node) anchor = movingEdge.ChildNode;
            else if (movingEdge.ChildNode == node) anchor = movingEdge.ParentNode;
            else continue;

            foreach (Node other in _simulationResult.Nodes)
            {
                if (other == node || other == anchor) continue;
                if (PointSegmentDistanceSqr(other.Position, target, anchor.Position) < minSqr ||
                    IsInsideSweptTriangle(other.Position, origin, target, anchor.Position)) return false;
            }
            foreach (NodeConnection fixedEdge in _simulationResult.NodeConnections)
            {
                if (fixedEdge.ParentNode == node || fixedEdge.ChildNode == node ||
                    fixedEdge.ParentNode == anchor || fixedEdge.ChildNode == anchor) continue;
                if (SegmentDistanceSqr(target, anchor.Position, fixedEdge.ParentNode.Position, fixedEdge.ChildNode.Position) < minSqr)
                    return false;
            }
        }
        // 공통 분기점 부근은 함께 써도 되지만 서로 다른 자식의 소유 반쪽은 겹치면 안 된다.
        List<NodeConnection> edges = _simulationResult.NodeConnections;
        for (int i = 0; i < edges.Count; i++) for (int j = i + 1; j < edges.Count; j++)
        {
            NodeConnection a = edges[i], b = edges[j];
            if (a.ParentNode != node && a.ChildNode != node && b.ParentNode != node && b.ChildNode != node) continue;
            if (TryGetSharedEndpoint(a, b, out Node shared, out Node endA, out Node endB))
            {
                Vector2 s = shared == node ? target : shared.Position;
                Vector2 pa = endA == node ? target : endA.Position;
                Vector2 pb = endB == node ? target : endB.Position;
                if (SegmentDistanceSqr((s + pa) * 0.5f, pa, (s + pb) * 0.5f, pb) < minSqr) return false;
            }
        }
        return true;
    }

    private float MeasureLayoutClearance()
    {
        float minimumSqr = float.PositiveInfinity;
        List<Node> nodes = _simulationResult.Nodes;
        for (int i = 0; i < nodes.Count; i++) for (int j = i + 1; j < nodes.Count; j++)
            minimumSqr = Mathf.Min(minimumSqr, (nodes[i].Position - nodes[j].Position).sqrMagnitude);
        List<NodeConnection> edges = _simulationResult.NodeConnections;
        foreach (NodeConnection edge in edges)
        {
            foreach (Node node in nodes)
                if (node != edge.ParentNode && node != edge.ChildNode)
                    minimumSqr = Mathf.Min(minimumSqr, PointSegmentDistanceSqr(node.Position, edge.ParentNode.Position, edge.ChildNode.Position));
        }
        for (int i = 0; i < edges.Count; i++) for (int j = i + 1; j < edges.Count; j++)
        {
            NodeConnection a = edges[i], b = edges[j];
            if (TryGetSharedEndpoint(a, b, out Node shared, out Node endA, out Node endB))
                minimumSqr = Mathf.Min(minimumSqr, SegmentDistanceSqr((shared.Position + endA.Position) * 0.5f, endA.Position,
                    (shared.Position + endB.Position) * 0.5f, endB.Position));
            else
                minimumSqr = Mathf.Min(minimumSqr, SegmentDistanceSqr(a.ParentNode.Position, a.ChildNode.Position, b.ParentNode.Position, b.ChildNode.Position));
        }
        return Mathf.Sqrt(minimumSqr);
    }

    private static bool TryGetSharedEndpoint(NodeConnection a, NodeConnection b, out Node shared, out Node endA, out Node endB)
    {
        shared = a.ParentNode == b.ParentNode || a.ParentNode == b.ChildNode ? a.ParentNode :
            a.ChildNode == b.ParentNode || a.ChildNode == b.ChildNode ? a.ChildNode : null;
        endA = shared == a.ParentNode ? a.ChildNode : a.ParentNode;
        endB = shared == b.ParentNode ? b.ChildNode : b.ParentNode;
        return shared != null;
    }

    private static float PointSegmentDistanceSqr(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float t = segment.sqrMagnitude > GEOMETRY_EPSILON ? Mathf.Clamp01(Vector2.Dot(p - a, segment) / segment.sqrMagnitude) : 0f;
        return (p - (a + segment * t)).sqrMagnitude;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static float SegmentDistanceSqr(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
        if (((abC > 0f && abD < 0f) || (abC < 0f && abD > 0f)) &&
            ((cdA > 0f && cdB < 0f) || (cdA < 0f && cdB > 0f))) return 0f;
        return Mathf.Min(Mathf.Min(PointSegmentDistanceSqr(a, c, d), PointSegmentDistanceSqr(b, c, d)),
            Mathf.Min(PointSegmentDistanceSqr(c, a, b), PointSegmentDistanceSqr(d, a, b)));
    }

    private static bool IsInsideSweptTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        if (Mathf.Abs(Cross(b - a, c - a)) < GEOMETRY_EPSILON) return false;
        float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
        return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
    }

    private async UniTask CutLoopConnections()
    {
        _simulationResult.NodeConnections.RemoveAll(edge => edge.ParentNode.Depth > edge.ChildNode.Depth);
        _simulationResult.RebuildAdjacency();
        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _ct);
    }

    private void FitNodesToTileGrid()
    {
        List<Node> nodes = _simulationResult.Nodes;
        if (nodes.Count == 0) return;
        float minX = nodes.Min(node => node.Position.x), maxX = nodes.Max(node => node.Position.x);
        float minY = nodes.Min(node => node.Position.y), maxY = nodes.Max(node => node.Position.y);
        float width = Mathf.Max(maxX - minX, MIN_DIMENSION_SIZE);
        float height = Mathf.Max(maxY - minY, MIN_DIMENSION_SIZE);
        float scale = Mathf.Min((_boundsMax.x - _boundsMin.x) / width, (_boundsMax.y - _boundsMin.y) / height);
        Vector2 sourceCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 targetCenter = (_boundsMin + _boundsMax) * 0.5f;
        foreach (Node node in nodes) node.Position = targetCenter + (node.Position - sourceCenter) * scale;
    }

    private ForceSimParameters CreateDynamicSettings(float unit, float lengthRatio, float repulsionFactor, int iterations)
    {
        float length = unit * lengthRatio;
        return new ForceSimParameters
        {
            simulationIterations = iterations,
            dampingFactor = 0.82f,
            idealEdgeLength = length,
            minNodeDistance = length * 0.4f,
            repulsionStrength = length * length * repulsionFactor,
            attractionStrength = 0.2f
        };
    }
}

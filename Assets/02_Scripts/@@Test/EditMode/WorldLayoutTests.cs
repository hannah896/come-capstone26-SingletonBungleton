using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>그래프 선 교차와 영토 분할 이후 실제 육로의 폭을 각각 검사합니다.</summary>
public class WorldLayoutTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> _assets = new();
    private object _burstOptions;
    private bool _burstWasEnabled;

    private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
    private static object New(string name, params object[] args) => Activator.CreateInstance(FindType(name), args);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, InstanceMembers)?.GetValue(obj) ??
        obj.GetType().GetProperty(name, InstanceMembers)?.GetValue(obj);
    private static void Set(object obj, string name, object value)
    {
        FieldInfo field = obj.GetType().GetField(name, InstanceMembers);
        if (field != null) field.SetValue(obj, value);
        else obj.GetType().GetProperty(name, InstanceMembers).SetValue(obj, value);
    }
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
    private object Asset(string type)
    {
        var asset = ScriptableObject.CreateInstance(FindType(type));
        _assets.Add(asset);
        return asset;
    }

    [SetUp] public void Setup()
    {
        // 네이티브 플러그인이 없는 테스트 환경에서도 동일한 Managed Jobs 경로를 검증합니다.
        Type burst = FindType("Unity.Burst.BurstCompiler");
        _burstOptions = burst?.GetField("Options")?.GetValue(null);
        if (_burstOptions == null) return;
        PropertyInfo enabled = _burstOptions.GetType().GetProperty("EnableBurstCompilation");
        _burstWasEnabled = (bool)enabled.GetValue(_burstOptions);
        enabled.SetValue(_burstOptions, false);
    }

    [TearDown] public void Cleanup()
    {
        if (_burstOptions != null)
            _burstOptions.GetType().GetProperty("EnableBurstCompilation").SetValue(_burstOptions, _burstWasEnabled);
        _burstOptions = null;
        foreach (var asset in _assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [UnityTest] public IEnumerator Simulation_PreservesBranches_WithoutCrossingsOrOtherNodePenetration()
    {
        var topologies = new[]
        {
            new[] { -1, 0, 0, 0, 1, 1, 2, 2, 3, 4, 6, 8, 8, 9 },
            new[] { -1, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { -1, 0, 1, 2, 3, 4, 5, 6 },
            new[] { -1, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 7, 10 }
        };
        foreach (int seed in new[] { 600284815, 73, -2100506294 })
        foreach (int[] parents in topologies)
        {
            object context = Context(512, parents, seed);
            var nodes = Nodes(context);
            var originalEdges = Edges(context).ToArray();
            yield return Run("ForceSimulator", context);
            Assert.That(Edges(context), Is.EqualTo(originalEdges), "지역의 부모/자식 관계를 바꾸면 안 됩니다.");
            AssertPlanarAndClear(context, 20f);
            foreach (object node in nodes)
            {
                var position = (Vector2)Get(node, "Position");
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x), Is.False);
                Assert.That(float.IsNaN(position.y) || float.IsInfinity(position.y), Is.False);
                Assert.That(position.x, Is.InRange(8f, 504f));
                Assert.That(position.y, Is.InRange(8f, 504f));
            }
        }
    }

    [UnityTest] public IEnumerator Simulation_LoopedStory_RemainsPlanarAfterExistingLoopCut()
    {
        object context = Context(512, new[] { -1, 0, 0, 1, 1, 2, 2, 3, 5 }, 600284815);
        object graph = Get(context, "GraphData");
        var nodes = Nodes(context);
        var edges = (IList)Get(graph, "NodeConnections");
        var treeEdges = edges.Cast<object>().ToArray();
        edges.Add(New("NodeConnection", nodes[7], nodes[0]));
        Set(graph, "IsLooped", true);
        yield return Run("ForceSimulator", context);
        Assert.That(Edges(context), Is.EqualTo(treeEdges));
        AssertPlanarAndClear(context, 20f);
    }

    [UnityTest] public IEnumerator Layout_IsDeterministic_IncludingProtectedTerritory()
    {
        int[] parents = { -1, 0, 0, 1, 1, 2, 2 };
        object first = Context(256, parents, 813), second = Context(256, parents, 813);
        foreach (object context in new[] { first, second })
        {
            yield return Run("ForceSimulator", context);
            yield return Run("TerritoryBuilder", context);
        }
        Assert.That(Nodes(first).Select(n => (Vector2)Get(n, "Position")).ToArray(),
            Is.EqualTo(Nodes(second).Select(n => (Vector2)Get(n, "Position")).ToArray()));
        Assert.That(Territory(first), Is.EqualTo(Territory(second)));
        foreach (object edge in Edges(first))
        {
            object a = Get(edge, "ParentNode"), b = Get(edge, "ChildNode");
            AssertWidePath(first, a, b, 8);
        }
    }

    [UnityTest] public IEnumerator Territory_ProtectsConfiguredWidth_DespiteNoiseAndCoastalCutting()
    {
        foreach (int width in new[] { 8, 16, 24 })
        foreach (bool diagonal in new[] { false, true })
        {
            object context = Context(128, new[] { -1, 0 }, 97);
            var nodes = Nodes(context);
            Set(nodes[0], "Position", diagonal ? new Vector2(18, 20) : new Vector2(14, 64));
            Set(nodes[1], "Position", diagonal ? new Vector2(108, 106) : new Vector2(114, 64));
            object partition = Get(Get(context, "Settings"), "PartitionSettings");
            Set(partition, "minimumConnectionWidth", width);
            Set(partition, "noiseStrength", 150f);
            Set(partition, "oceanThreshold", 0.1f);
            yield return Run("TerritoryBuilder", context);
            AssertWidePath(context, nodes[0], nodes[1], width / 2);
            Assert.That(Territory(context)[0, 0], Is.EqualTo(-1), "육로 보호가 맵 전체를 육지로 바꾸면 안 됩니다.");
            AssertOwnedTilesAgree(context);
            // 보호된 땅이 영향력 계산에도 반영되어야 해안 처리/후속 높이 생성이 같은 지도를 사용합니다.
            var coast = (int[,])Get(Get(context, "LogicData"), "DistanceToOceanWorld");
            var start = Vector2Int.RoundToInt((Vector2)Get(nodes[0], "Position"));
            Assert.That(coast[start.x, start.y], Is.GreaterThanOrEqualTo(width / 2));
        }
    }

    [UnityTest] public IEnumerator Territory_DoesNotBridgeSeparateUnconnectedBranches()
    {
        object context = Context(128, new[] { -1, 0, -1, 2 }, 218);
        var nodes = Nodes(context);
        Vector2[] positions = { new(18, 28), new(110, 28), new(18, 100), new(110, 100) };
        for (int i = 0; i < nodes.Count; i++) Set(nodes[i], "Position", positions[i]);
        object partition = Get(Get(context, "Settings"), "PartitionSettings");
        Set(partition, "minimumConnectionWidth", 16);
        Set(partition, "noiseStrength", 150f);
        Set(partition, "oceanThreshold", 0.1f);
        yield return Run("TerritoryBuilder", context);
        AssertWidePath(context, nodes[0], nodes[1], 8);
        AssertWidePath(context, nodes[2], nodes[3], 8);
        int[,] territory = Territory(context);
        for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++)
        {
            int owner = territory[x, y];
            if (owner < 0) continue;
            foreach (var step in new[] { Vector2Int.right, Vector2Int.up })
            {
                int nx = x + step.x, ny = y + step.y;
                if (nx >= 128 || ny >= 128 || territory[nx, ny] < 0) continue;
                Assert.That(territory[nx, ny] / 2, Is.EqualTo(owner / 2), "서로 연결되지 않은 두 분기를 육로로 합치면 안 됩니다.");
            }
        }
    }

    [UnityTest] public IEnumerator SingleRegion_HasFinitePlacementAndUsableLand()
    {
        object context = Context(128, new[] { -1 }, 11);
        yield return Run("ForceSimulator", context);
        yield return Run("TerritoryBuilder", context);
        var position = (Vector2)Get(Nodes(context)[0], "Position");
        Assert.That(Vector2.Distance(position, new Vector2(64.5f, 64.5f)), Is.LessThan(2f));
        object logic = Get(context, "LogicData");
        var spawn = (Vector2Int)Get(logic, "SpawnTile");
        Assert.That(spawn.x, Is.InRange(0, 127));
        Assert.That(spawn.y, Is.InRange(0, 127));
        Assert.That(Territory(context)[spawn.x, spawn.y], Is.EqualTo(0));
        AssertOwnedTilesAgree(context);
    }

    private object Context(int size, int[] parents, int seed)
    {
        object settings = Asset("WorldSettings");
        Set(settings, "WorldSeed", seed);
        Set(settings, "worldSmall", size - 1);
        Set(settings, "WorldSize", Enum.Parse(FindType("WorldSize"), "Small"));
        Set(settings, "_enableStepByStep", false);
        var simulation = Get(settings, "ForceSimulatorSettings");
        Set(simulation, "macroLengthRatio", 1f);
        Set(simulation, "macroRepulsionFactor", 2f);
        Set(simulation, "maxIterations", 200);
        var partition = Get(settings, "PartitionSettings");
        Set(partition, "minimumConnectionWidth", 16);
        Set(partition, "batchSize", 100000);
        object graph = New("WorldGraphData");
        var nodes = (IList)Get(graph, "Nodes");
        for (int i = 0; i < parents.Length; i++)
        {
            object region = Asset("RegionData");
            Set(region, "RegionName", "LayoutTestRegion" + i);
            Set(region, "TerritoryScale", 1f + (i % 3) * 0.5f);
            object node = New("Node");
            Set(node, "Index", i);
            Set(node, "Depth", parents[i] < 0 ? 1 : (int)Get(nodes[parents[i]], "Depth") + 1);
            Set(node, "RegionData", region);
            Set(node, "Position", new Vector2(1f, 1f));
            Set(node, "Velocity", new Vector2(123f, -456f));
            nodes.Add(node);
            if (parents[i] >= 0) ((IList)Get(graph, "NodeConnections")).Add(New("NodeConnection", nodes[parents[i]], node));
        }
        Call(graph, "RebuildAdjacency");
        object story = Asset("StoryData");
        Set(story, "StartRegion", Get(nodes[0], "RegionData"));
        Set(settings, "CurrentStory", story);
        object context = New("WorldGenContext");
        Set(context, "Settings", settings);
        Set(context, "GraphData", graph);
        Set(context, "LogicData", New("WorldLogicData", new Vector2Int(size, size), 16));
        Set(context, "DisposeData", New("WorldDisposeData"));
        return context;
    }

    private static IEnumerator Run(string stageName, object context)
    {
        object stage = New(stageName);
        Call(stage, "Initialize", Get(context, "Settings"));
        object task = Call(stage, "ExecuteAsync", context, CancellationToken.None);
        int frames = 0;
        while (Get(task, "Status").ToString() == "Pending")
        {
            Assert.That(++frames, Is.LessThan(10000), stageName + "이 종료되지 않았습니다.");
            yield return null;
        }
        Call(Call(task, "GetAwaiter"), "GetResult");
    }

    private static List<object> Nodes(object context) => ((IEnumerable)Get(Get(context, "GraphData"), "Nodes")).Cast<object>().ToList();
    private static List<object> Edges(object context) => ((IEnumerable)Get(Get(context, "GraphData"), "NodeConnections")).Cast<object>().ToList();
    private static int[,] Territory(object context) => (int[,])Get(Get(context, "LogicData"), "TerritoryWorld");

    private static void AssertPlanarAndClear(object context, float minimumClearance)
    {
        var edges = Edges(context);
        var nodes = Nodes(context);
        static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;
        foreach (object edge in edges)
        {
            object a = Get(edge, "ParentNode"), b = Get(edge, "ChildNode");
            Vector2 p = (Vector2)Get(a, "Position"), q = (Vector2)Get(b, "Position");
            Assert.That(Vector2.Distance(p, q), Is.GreaterThan(0.1f));
            foreach (object node in nodes.Where(n => n != a && n != b))
            {
                Vector2 point = (Vector2)Get(node, "Position");
                float t = Mathf.Clamp01(Vector2.Dot(point - p, q - p) / (q - p).sqrMagnitude);
                Assert.That(Vector2.Distance(point, Vector2.Lerp(p, q, t)), Is.GreaterThanOrEqualTo(minimumClearance - 0.001f),
                    "연결선이 다른 지역 중심에 너무 가까이 지나갑니다.");
            }
            foreach (object other in edges)
            {
                if (ReferenceEquals(edge, other)) continue;
                object c = Get(other, "ParentNode"), d = Get(other, "ChildNode");
                object shared = a == c || a == d ? a : b == c || b == d ? b : null;
                if (shared != null)
                {
                    Vector2 hub = (Vector2)Get(shared, "Position");
                    Vector2 firstEnd = (Vector2)Get(a == shared ? b : a, "Position");
                    Vector2 secondEnd = (Vector2)Get(c == shared ? d : c, "Position");
                    Vector2 firstHalf = Vector2.Lerp(hub, firstEnd, 0.5f);
                    Vector2 secondHalf = Vector2.Lerp(hub, secondEnd, 0.5f);
                    float distance = Mathf.Min(PointSegmentDistance(firstHalf, secondHalf, secondEnd),
                        PointSegmentDistance(firstEnd, secondHalf, secondEnd),
                        PointSegmentDistance(secondHalf, firstHalf, firstEnd),
                        PointSegmentDistance(secondEnd, firstHalf, firstEnd));
                    Assert.That(distance, Is.GreaterThanOrEqualTo(minimumClearance - 0.001f),
                        "공통 부모의 서로 다른 자식 소유 육로가 겹칠 만큼 가까워집니다.");
                    continue;
                }
                Vector2 r = (Vector2)Get(c, "Position"), s = (Vector2)Get(d, "Position");
                bool intersects = Cross(q - p, r - p) * Cross(q - p, s - p) < 0d &&
                    Cross(s - r, p - r) * Cross(s - r, q - r) < 0d;
                Assert.That(intersects, Is.False, "부모/자식을 공유하지 않는 두 연결선이 교차합니다.");
            }
        }
    }

    private static float PointSegmentDistance(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float t = direction.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, direction) / direction.sqrMagnitude) : 0f;
        return Vector2.Distance(point, from + t * direction);
    }

    private static void AssertWidePath(object context, object startNode, object endNode, int radius)
    {
        int[,] owners = Territory(context);
        int width = owners.GetLength(0), height = owners.GetLength(1);
        var nodes = Nodes(context);
        int startOwner = nodes.IndexOf(startNode), endOwner = nodes.IndexOf(endNode);
        Vector2Int start = Vector2Int.RoundToInt((Vector2)Get(startNode, "Position"));
        Vector2Int end = Vector2Int.RoundToInt((Vector2)Get(endNode, "Position"));
        var offsets = new List<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++) for (int dy = -radius; dy <= radius; dy++)
            if (dx * dx + dy * dy <= radius * radius) offsets.Add(new Vector2Int(dx, dy));
        // 바다/제3 지역을 원형 플레이어 발판만큼 침식한 뒤 실제 4방향 경로가 남는지 검사합니다.
        bool CanStand(Vector2Int tile)
        {
            foreach (var delta in offsets)
            {
                Vector2Int p = tile + delta;
                if (p.x < 0 || p.y < 0 || p.x >= width || p.y >= height) return false;
                int owner = owners[p.x, p.y];
                if (owner != startOwner && owner != endOwner) return false;
            }
            return true;
        }
        Assert.That(CanStand(start), Is.True, "육로의 시작 지점에 설정 폭의 발판이 없습니다.");
        Assert.That(CanStand(end), Is.True, "육로의 도착 지점에 설정 폭의 발판이 없습니다.");
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int> { start };
        queue.Enqueue(start);
        var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            if (tile == end) return;
            foreach (var direction in directions)
            {
                Vector2Int next = tile + direction;
                if (!visited.Add(next) || !CanStand(next)) continue;
                queue.Enqueue(next);
            }
        }
        Assert.Fail("지역 중심 사이에 지름 " + (radius * 2) + "타일의 연속 육로가 없습니다.");
    }

    private static void AssertOwnedTilesAgree(object context)
    {
        int[,] owners = Territory(context);
        var nodes = Nodes(context);
        var seen = new HashSet<Vector2Int>();
        for (int i = 0; i < nodes.Count; i++)
        foreach (Vector2Int tile in (IEnumerable)Get(nodes[i], "OwnedTiles"))
        {
            Assert.That(owners[tile.x, tile.y], Is.EqualTo(i));
            Assert.That(seen.Add(tile), Is.True);
        }
        int landCount = 0;
        foreach (int owner in owners) if (owner >= 0) landCount++;
        Assert.That(seen.Count, Is.EqualTo(landCount), "후속 지형 생성이 보호된 육로 타일을 누락하면 안 됩니다.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>기존 배치의 난수열/ID를 바꾸지 않고 마지막에 지형 텔레포트를 추가합니다.</summary>
public sealed class TeleportDisposer : IGraphPipelineStage
{
    private WorldSettings _settings;
    private WorldLogicData _logic;
    private WorldGraphData _graph;
    private System.Random _random;
    private readonly HashSet<int> _ids = new();
    private readonly Dictionary<Vector2Int, (Vector2Int tile, Vector2Int arrival)> _pads = new();
    private int[,] _branchDistance;
    private readonly Dictionary<int, int> _branchThresholds = new();
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    public void Initialize(WorldSettings settings) => _settings = settings;

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _logic = ctx.LogicData;
        _graph = ctx.GraphData;
        _logic.Teleports.Clear();
        _pads.Clear();
        _branchThresholds.Clear();
        _branchDistance = new int[_logic.TerrainSize.x, _logic.TerrainSize.y];
        _ids.Clear();
        foreach (var dispose in ctx.DisposeData.DisposeDatas) _ids.Add(dispose.instanceId);
        foreach (var monster in ctx.DisposeData.InitialMonsterPlacements) _ids.Add(monster.id);
        _random = new System.Random(unchecked(_settings.WorldSeed + 811));
        _settings.TeleportSettings ??= new WorldTeleportSettings();

        // Highlands는 전체 월드에 정확히 한 연결을 만듭니다.
        var highlands = _graph.Nodes.Where(IsHighlands).ToList();
        bool placed = false;
        foreach (Node highland in highlands)
        {
            int highIndex = _graph.Nodes.IndexOf(highland);
            var approachable = GetApproachRegions(highland);
            // 요구 키를 얻는 낮은 티어 지역을 우선하고, 같은 티어에서는 직접 연결된 지역을 우선합니다.
            foreach (Node other in approachable.Where(n => ProvidesHighlandsKeys(n, highland))
                         .OrderByDescending(n => n.RegionData.RequiredTier)
                         .ThenBy(n => Connected(n, highland) ? 0 : 1)
                         .ThenBy(n => (n.Position - highland.Position).sqrMagnitude).ThenBy(n => n.Index))
            {
                ct.ThrowIfCancellationRequested();
                int otherIndex = _graph.Nodes.IndexOf(other);
                if (!TryFindPad(highIndex, other.Position, out var a, out var aa) ||
                    !TryFindPad(otherIndex, highland.Position, out var b, out var ba)) continue;
                AddPair(highIndex, a, aa, otherIndex, b, ba, true);
                placed = true;
                break;
            }
            if (placed) break;
            await UniTask.Yield(ct);
        }
        if (highlands.Count > 0 && !placed)
            throw new InvalidOperationException("Highlands보다 낮은 티어에서 요구 키를 제공하는 접근 가능한 지역에 필수 텔레포트 발판을 확보하지 못했습니다.");

        var ends = _graph.Nodes.Where(n => n.Depth > 1 && !IsHighlands(n) &&
            !_graph.NodeConnections.Any(c => c.ParentNode == n && c.ChildNode.Depth > n.Depth))
            .Select(n => _graph.Nodes.IndexOf(n)).OrderBy(i => i).ToList();
        var shores = ends.ToDictionary(i => i, _ => new List<Vector2Int>());
        var entries = ends.ToDictionary(i => i, _ => new List<Vector2Int>());
        var parents = ends.ToDictionary(i => i, i => _graph.Nodes.IndexOf(_graph.GetParentNode(_graph.Nodes[i])));
        var touching = new HashSet<(int, int)>();
        for (int x = 0; x < _logic.TerrainSize.x; x++)
        {
            for (int y = 0; y < _logic.TerrainSize.y; y++)
            {
                int owner = _logic.GetRegionAt(x, y);
                if (owner < 0) continue;
                bool coastal = false;
                bool entry = false;
                foreach (Vector2Int direction in Directions)
                {
                    int neighbor = _logic.GetRegionAt(x + direction.x, y + direction.y);
                    if (neighbor == TerritoryBuilder.OCEAN_MARKER && Inside(x + direction.x, y + direction.y)) coastal = true;
                    if (neighbor >= 0 && neighbor != owner) touching.Add((Math.Min(owner, neighbor), Math.Max(owner, neighbor)));
                    if (neighbor >= 0 && parents.TryGetValue(owner, out int parent) && neighbor == parent) entry = true;
                }
                if (coastal && shores.TryGetValue(owner, out var shore)) shore.Add(new Vector2Int(x, y));
                if (entry) entries[owner].Add(new Vector2Int(x, y));
            }
            if (x % 32 == 0) await UniTask.Yield(ct);
        }
        foreach (int region in ends)
        {
            await MeasureBranchDepthAsync(region, parents[region], entries[region], ct);
            shores[region].RemoveAll(tile => !IsDeepInBranch(region, tile));
        }

        var candidates = new List<Link>();
        for (int i = 0; i < ends.Count; i++)
        {
            for (int j = i + 1; j < ends.Count; j++)
            {
                int a = ends[i], b = ends[j];
                if (touching.Contains((a, b)) || shores[a].Count == 0 || shores[b].Count == 0) continue;
                float maxDistance = Mathf.Max(1f, _settings.TeleportSettings.shortcutMaxDistance);
                Link link = new() { a = a, b = b, distance = maxDistance * maxDistance };
                // 충분히 진입한 해안 중 거리 범위와 바다 경로를 만족하는 최근접 쌍만 탐색합니다.
                var tree = CoastTree.Build(shores[b].ToArray(), 0);
                int work = 0;
                foreach (var coast in shores[a])
                {
                    tree.Find(coast, this, ref link);
                    if (++work % 128 == 0) await UniTask.Yield(ct);
                }
                if (link.found) candidates.Add(link);
            }
        }
        candidates.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance) :
            a.a != b.a ? a.a.CompareTo(b.a) : a.b.CompareTo(b.b));
        var counts = ends.ToDictionary(i => i, _ => 0);
        foreach (Link link in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (counts[link.a] >= 2 || counts[link.b] >= 2) continue;
            // 앞선 연결과 겹칠 경우 같은 해안 근처에서 별도 발판을 확보합니다.
            if (!TryNearPad(link.a, link.coastA, out var a, out var aa) ||
                !TryNearPad(link.b, link.coastB, out var b, out var ba)) continue;
            if (!WithinShortcutRange((a - b).sqrMagnitude)) continue;
            AddPair(link.a, a, aa, link.b, b, ba, false);
            counts[link.a]++;
            counts[link.b]++;
        }

        WorldTeleportPlacement.Validate(_logic.Teleports, _logic);
        foreach (var entry in _logic.Teleports)
        {
            DisposeData dispose = entry.CreateDispose();
            ctx.DisposeData.DisposeDatas.Add(dispose);
            ctx.DisposeData.ObjectDisposes.Add(dispose);
        }
        Debug.Log($"[Teleport] Highlands {(_logic.Teleports.Count(p => p.highlands) / 2)}쌍, 숏컷 {_logic.Teleports.Count(p => !p.highlands) / 2}쌍 배치");
    }

    private bool Connected(Node a, Node b) => _graph.NodeConnections.Any(c =>
        c.ParentNode == a && c.ChildNode == b || c.ParentNode == b && c.ChildNode == a);
    private static bool IsHighlands(Node n) => n.BiomeData != null && n.BiomeData.BiomeType == BiomeType.Highlands;
    private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < _logic.TerrainSize.x && y < _logic.TerrainSize.y;

    private HashSet<Node> GetApproachRegions(Node highland)
    {
        int highestTier = Math.Max(0, highland.RegionData.RequiredTier - 1);
        var reachable = new HashSet<Node>();
        var queue = new Queue<Node>();
        foreach (var start in _graph.Nodes.Where(n => n.Depth == 1 && !IsHighlands(n) &&
                     n.RegionData != null && n.RegionData.RequiredTier <= highestTier))
        {
            reachable.Add(start);
            queue.Enqueue(start);
        }
        while (queue.Count > 0)
        {
            Node node = queue.Dequeue();
            foreach (var connection in _graph.NodeConnections)
            {
                Node neighbor = connection.ParentNode == node ? connection.ChildNode :
                    connection.ChildNode == node ? connection.ParentNode : null;
                if (neighbor == null || IsHighlands(neighbor) || neighbor.RegionData == null ||
                    neighbor.RegionData.RequiredTier > highestTier || !reachable.Add(neighbor)) continue;
                queue.Enqueue(neighbor);
            }
        }
        return reachable;
    }

    private static bool ProvidesHighlandsKeys(Node source, Node highland)
    {
        var locks = highland.RegionData.LockIDs;
        if (locks == null || locks.Count == 0) return true;
        var keys = source.RegionData.GivenKeyIDs;
        return locks.All(id => id == Locks.LOCK_NONE || keys != null && LockResolver.CanUnlock(id, keys));
    }

    private async UniTask MeasureBranchDepthAsync(int region, int parent, List<Vector2Int> entries, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // 실제 육상 연결부를 입구로 사용합니다. 떨어진 섬은 부모에 가장 가까운 소유 타일부터 측정합니다.
        if (entries.Count == 0)
        {
            Vector3 spawn = WorldSpawnArea.GetCenter(_logic);
            Vector2 origin = parent >= 0 ? _graph.Nodes[parent].Position : new Vector2(spawn.x, spawn.z);
            float best = float.PositiveInfinity;
            Vector2Int closest = default;
            foreach (var tile in _graph.Nodes[region].OwnedTiles)
            {
                if (_logic.GetRegionAt(tile.x, tile.y) != region) continue;
                float distance = ((Vector2)tile - origin).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; closest = tile;
            }
            if (!float.IsPositiveInfinity(best)) entries.Add(closest);
        }
        var queue = new Queue<Vector2Int>();
        foreach (var tile in entries)
        {
            _branchDistance[tile.x, tile.y] = 1;
            queue.Enqueue(tile);
        }
        int maximum = 1, work = 0;
        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            int nextDistance = _branchDistance[tile.x, tile.y] + 1;
            foreach (var direction in Directions)
            {
                Vector2Int next = tile + direction;
                if (_logic.GetRegionAt(next.x, next.y) != region || _branchDistance[next.x, next.y] != 0) continue;
                _branchDistance[next.x, next.y] = nextDistance;
                maximum = Math.Max(maximum, nextDistance);
                queue.Enqueue(next);
            }
            if (++work % 4096 == 0) await UniTask.Yield(ct);
        }
        _branchThresholds[region] = 1 + Mathf.CeilToInt((maximum - 1) *
            Mathf.Clamp01(_settings.TeleportSettings.shortcutMinBranchProgress));
    }

    private bool IsDeepInBranch(int region, Vector2Int tile) =>
        Inside(tile.x, tile.y) && _branchDistance[tile.x, tile.y] >= _branchThresholds[region];

    private bool WithinShortcutRange(float squaredDistance)
    {
        float min = Mathf.Max(1f, _settings.TeleportSettings.shortcutMinSeaDistance);
        float max = Mathf.Max(1f, _settings.TeleportSettings.shortcutMaxDistance);
        return squaredDistance >= min * min && squaredDistance <= max * max;
    }

    private bool Flat(int region, Vector2Int tile, int radius)
    {
        if (!Inside(tile.x, tile.y)) return false;
        float height = _logic.HeightWorld[tile.x, tile.y];
        float rise = Mathf.Tan(_settings.TeleportSettings.maxSlope * Mathf.Deg2Rad);
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                if (_logic.GetRegionAt(tile.x + x, tile.y + y) != region) return false;
                if (Mathf.Abs(_logic.HeightWorld[tile.x + x, tile.y + y] - height) >
                    Mathf.Max(0.25f, rise * new Vector2(x, y).magnitude)) return false;
            }
        return true;
    }

    private bool Pad(int region, Vector2Int tile, out Vector2Int arrival)
    {
        arrival = default;
        var position = new Vector3(tile.x, 0f, tile.y);
        if (WorldSpawnArea.Contains(_logic, position, 4f) || WorldTeleportPlacement.Contains(_logic, position, 4f) ||
            !Flat(region, tile, 2)) return false;
        foreach (Vector2Int direction in Directions)
        {
            var candidate = tile + direction * 3;
            if (!Flat(region, candidate, 1) ||
                WorldTeleportPlacement.Contains(_logic, new Vector3(candidate.x, 0f, candidate.y), 2f)) continue;
            arrival = candidate;
            return true;
        }
        return false;
    }

    private bool TryFindPad(int region, Vector2 near, out Vector2Int tile, out Vector2Int arrival)
    {
        tile = arrival = default;
        float best = float.PositiveInfinity;
        bool found = false;
        foreach (var candidate in _graph.Nodes[region].OwnedTiles)
        {
            float distance = ((Vector2)candidate - near).sqrMagnitude;
            if (distance >= best || !Pad(region, candidate, out var landing)) continue;
            tile = candidate; arrival = landing; best = distance; found = true;
        }
        return found;
    }

    private bool TryNearPad(int region, Vector2Int coast, out Vector2Int tile, out Vector2Int arrival)
    {
        tile = arrival = default;
        float best = float.PositiveInfinity;
        bool found = false;
        for (int x = -12; x <= 12; x++)
            for (int y = -12; y <= 12; y++)
            {
                int distance = x * x + y * y;
                if (distance >= best) continue;
                var candidate = coast + new Vector2Int(x, y);
                if (!IsDeepInBranch(region, candidate) || !Pad(region, candidate, out var landing)) continue;
                tile = candidate; arrival = landing; best = distance; found = true;
            }
        return found;
    }

    private bool TryCachedPad(int region, Vector2Int coast, out Vector2Int tile)
    {
        if (!_pads.TryGetValue(coast, out var cached))
        {
            bool found = TryNearPad(region, coast, out var pad, out var arrival);
            cached = found ? (pad, arrival) : (new Vector2Int(-1, -1), default);
            _pads[coast] = cached;
        }
        tile = cached.tile;
        return tile.x >= 0;
    }

    private bool AcrossWater(int regionA, Vector2Int a, int regionB, Vector2Int b)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
        bool ocean = false, reachedB = false;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / Mathf.Max(1, steps));
            int region = _logic.GetRegionAt(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (region == TerritoryBuilder.OCEAN_MARKER)
            {
                if (reachedB) return false;
                ocean = true;
            }
            else if (region == regionA) { if (ocean) return false; }
            else if (region == regionB) { if (!ocean) return false; reachedB = true; }
            else return false;
        }
        return ocean && reachedB;
    }

    private void AddPair(int a, Vector2Int tileA, Vector2Int arrivalA, int b, Vector2Int tileB, Vector2Int arrivalB, bool highlands)
    {
        var first = Entry(a, tileA, arrivalA, highlands);
        var second = Entry(b, tileB, arrivalB, highlands);
        first.targetId = second.id; second.targetId = first.id;
        _logic.Teleports.Add(first); _logic.Teleports.Add(second);
    }

    private WorldTeleportPlacement Entry(int region, Vector2Int tile, Vector2Int arrival, bool highlands)
    {
        int id;
        do { id = _random.Next(1, int.MaxValue); } while (!_ids.Add(id));
        var cost = highlands ? TeleportCost.None : (TeleportCost)_random.Next(1, 4);
        return new WorldTeleportPlacement { id = id, regionIndex = region, tile = tile, arrivalTile = arrival,
            highlands = highlands, cost = cost, amount = _settings.TeleportSettings.Amount(cost) };
    }

    private struct Link
    {
        public int a, b;
        public float distance;
        public Vector2Int coastA, coastB;
        public bool found;
    }

    private sealed class CoastTree
    {
        private Vector2Int _point;
        private int _axis;
        private CoastTree _left, _right;

        public static CoastTree Build(Vector2Int[] points, int depth)
        {
            if (points.Length == 0) return null;
            int axis = depth % 2;
            Array.Sort(points, (a, b) => axis == 0 ? (a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y)) :
                (a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x)));
            int mid = points.Length / 2;
            return new CoastTree { _point = points[mid], _axis = axis,
                _left = Build(points.Take(mid).ToArray(), depth + 1),
                _right = Build(points.Skip(mid + 1).ToArray(), depth + 1) };
        }

        public void Find(Vector2Int a, TeleportDisposer owner, ref Link best)
        {
            float distance = (_point - a).sqrMagnitude;
            if ((distance < best.distance || !best.found && distance == best.distance) &&
                owner.WithinShortcutRange(distance) && owner.AcrossWater(best.a, a, best.b, _point) &&
                owner.TryCachedPad(best.a, a, out var padA) && owner.TryCachedPad(best.b, _point, out var padB) &&
                owner.WithinShortcutRange((padA - padB).sqrMagnitude))
            {
                best.distance = distance; best.coastA = a; best.coastB = _point; best.found = true;
            }
            int delta = _axis == 0 ? a.x - _point.x : a.y - _point.y;
            CoastTree near = delta < 0 ? _left : _right, far = delta < 0 ? _right : _left;
            near?.Find(a, owner, ref best);
            if ((float)delta * delta <= best.distance) far?.Find(a, owner, ref best);
        }
    }
}

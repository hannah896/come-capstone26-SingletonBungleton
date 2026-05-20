using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ItemDisposer : IGraphPipelineStage
{
    private WorldSettings _worldSettings;
    private DisposeSettings _disposeSettings;

    private WorldDisposeData _disposeData;
    private WorldLogicData _logicData;
    private CancellationToken _ct;

    private List<Node> _nodes;
    private Node _currentNode;

    private System.Random _prng;
    private int _placementSequence;

    private Dictionary<SpawnShape, IDisposePatternStrategy> _patternStrategies;

    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _disposeSettings = _worldSettings.DisposeSettings;
        _prng = new System.Random(_worldSettings.WorldSeed + (int)WorldSeedChannel.ObjectDisposer);

        _patternStrategies = new Dictionary<SpawnShape, IDisposePatternStrategy>
        {
            { SpawnShape.Default, new DefaultPatternStrategy() },
            { SpawnShape.Line, new LinePatternStrategy() },
            { SpawnShape.Curve, new CurvePatternStrategy() },
            { SpawnShape.Branching, new BranchingPatternStrategy() }
        };
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _disposeData = ctx.DisposeData;
        _logicData = ctx.LogicData;
        _ct = ct;
        _nodes = ctx.GraphData?.Nodes;

        _placementSequence = ctx.DisposeData.DisposeDatas.Count;

        if (_nodes == null) return;

        foreach (Node node in _nodes)
        {
            _ct.ThrowIfCancellationRequested();

            _currentNode = node;
            await PlaceItemsForNodeAsync();
        }
    }

    private async UniTask PlaceItemsForNodeAsync()
    {
        PlacementRuleSet ruleSet = _currentNode.RegionData.RegionPlacementRule;
        if (ruleSet == null || ruleSet.ItemRules == null || ruleSet.ItemRules.Count == 0)
            return;

        List<Vector2Int> availablePositions = BuildAvailablePositions(_currentNode.CandidatePoints);
        if (availablePositions.Count == 0) return;

        foreach (ItemRule rule in ruleSet.ItemRules)
        {
            if (rule == null) continue;

            string prefabKey = rule.prefabKey;
            if (string.IsNullOrEmpty(prefabKey)) continue;

            if (rule.usePiece)
            {
                await PlacePiecesAsync(rule, prefabKey, availablePositions);
            }

            if (rule.useCluster)
            {
                await PlaceClustersAsync(rule, prefabKey, availablePositions);
            }
        }
    }

    private async UniTask PlacePiecesAsync(ItemRule rule, string prefabKey, List<Vector2Int> availablePositions)
    {
        if (rule.pieceShapes == null || rule.pieceShapes.Count == 0) return;

        foreach (SpawnShapeCount entry in rule.pieceShapes)
        {
            _ct.ThrowIfCancellationRequested();
            if (availablePositions.Count == 0) break;

            int count = Mathf.Min(entry.maxCount, availablePositions.Count);
            if (count <= 0) continue;

            if (entry.shape == SpawnShape.Default)
            {
                for (int i = 0; i < count && availablePositions.Count > 0; i++)
                {
                    Vector2Int tile = TakeRandomPosition(availablePositions);
                    PlaceAndOccupy(prefabKey, tile, availablePositions);
                }

                continue;
            }

            Vector2Int center = TakeRandomPosition(availablePositions);
            List<Vector2Int> placements = new List<Vector2Int> { center };

            int extraCount = Mathf.Min(count - 1, availablePositions.Count);
            if (extraCount > 0)
            {
                List<Vector2Int> extra = await TakePatternPositionsAsync(
                    availablePositions,
                    center,
                    entry.shape,
                    rule,
                    rule.patternLength,
                    extraCount
                );
                placements.AddRange(extra);
            }

            for (int i = 0; i < placements.Count; i++)
            {
                PlaceAndOccupy(prefabKey, placements[i], availablePositions);
            }
        }
    }

    private async UniTask PlaceClustersAsync(ItemRule rule, string prefabKey, List<Vector2Int> availablePositions)
    {
        if (rule.clusterShapes == null || rule.clusterShapes.Count == 0) return;

        float clusterPatternLength = GetClusterPatternLength(rule);

        foreach (SpawnShapeCount entry in rule.clusterShapes)
        {
            _ct.ThrowIfCancellationRequested();
            if (availablePositions.Count == 0) break;

            int maxClusterCount = Mathf.Max(0, entry.maxCount);
            if (maxClusterCount <= 0) continue;

            int clusterCount = _prng.Next(1, maxClusterCount + 1);
            clusterCount = Mathf.Min(clusterCount, availablePositions.Count);

            List<Vector2Int> clusterCenters = new List<Vector2Int>();
            if (clusterCount <= 0) continue;

            if (entry.shape == SpawnShape.Default)
            {
                for (int i = 0; i < clusterCount && availablePositions.Count > 0; i++)
                {
                    clusterCenters.Add(TakeRandomPosition(availablePositions));
                }
            }
            else
            {
                Vector2Int center = TakeRandomPosition(availablePositions);
                clusterCenters.Add(center);

                int extraCount = Mathf.Min(clusterCount - 1, availablePositions.Count);
                if (extraCount > 0)
                {
                    List<Vector2Int> extraCenters = await TakePatternPositionsAsync(
                        availablePositions,
                        center,
                        entry.shape,
                        rule,
                        clusterPatternLength,
                        extraCount
                    );
                    clusterCenters.AddRange(extraCenters);
                }
            }

            for (int i = 0; i < clusterCenters.Count && availablePositions.Count > 0; i++)
            {
                Vector2Int center = clusterCenters[i];

                int spawnCount = GetSpawnCount(rule);
                spawnCount = Mathf.Min(spawnCount, availablePositions.Count + 1);

                PlaceAndOccupy(prefabKey, center, availablePositions);

                if (spawnCount <= 1 || rule.clusterRadius <= 0f) continue;

                List<Vector2Int> extra = await TakeClusterPositionsAsync(
                    availablePositions,
                    center,
                    rule.clusterRadius,
                    spawnCount - 1
                );

                for (int j = 0; j < extra.Count; j++)
                {
                    PlaceAndOccupy(prefabKey, extra[j], availablePositions);
                }
            }
        }
    }

    private List<Vector2Int> BuildAvailablePositions(List<Vector2Int> candidatePoints)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (candidatePoints == null || candidatePoints.Count == 0) return result;

        for (int i = 0; i < candidatePoints.Count; i++)
        {
            Vector2Int pos = candidatePoints[i];
            if (!IsOccupied(pos))
            {
                result.Add(pos);
            }
        }

        return result;
    }

    private bool IsOccupied(Vector2Int pos)
    {
        if (_logicData == null || _logicData.OccupiedWorld == null) return false;
        if (pos.x < 0 || pos.x >= _logicData.TerrainSize.x || pos.y < 0 || pos.y >= _logicData.TerrainSize.y)
            return true;

        return _logicData.OccupiedWorld[pos.x, pos.y];
    }

    private void PlaceAndOccupy(string prefabKey, Vector2Int tile, List<Vector2Int> availablePositions)
    {
        AddPlacement(prefabKey, tile);

        float radius = Mathf.Max(0f, _disposeSettings.minObjectDistance);
        MarkOccupied(tile, radius);
        RemoveOccupiedCandidates(availablePositions, tile, radius);
    }

    private void MarkOccupied(Vector2Int center, float radius)
    {
        if (_logicData == null || _logicData.OccupiedWorld == null) return;

        int radiusCeil = Mathf.CeilToInt(radius);
        float sqrRadius = radius * radius;

        int minX = Mathf.Max(0, center.x - radiusCeil);
        int maxX = Mathf.Min(_logicData.TerrainSize.x - 1, center.x + radiusCeil);
        int minY = Mathf.Max(0, center.y - radiusCeil);
        int maxY = Mathf.Min(_logicData.TerrainSize.y - 1, center.y + radiusCeil);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                if ((dx * dx) + (dy * dy) <= sqrRadius)
                {
                    _logicData.OccupiedWorld[x, y] = true;
                }
            }
        }
    }

    private void RemoveOccupiedCandidates(List<Vector2Int> availablePositions, Vector2Int center, float radius)
    {
        if (availablePositions == null || availablePositions.Count == 0) return;

        float sqrRadius = radius * radius;

        for (int i = availablePositions.Count - 1; i >= 0; i--)
        {
            Vector2Int pos = availablePositions[i];
            float dx = pos.x - center.x;
            float dy = pos.y - center.y;
            if ((dx * dx) + (dy * dy) <= sqrRadius)
            {
                availablePositions.RemoveAt(i);
            }
        }
    }

    private float GetClusterPatternLength(ItemRule rule)
    {
        float length = rule.patternLength + (rule.clusterRadius * 2f);
        return Mathf.Max(1f, length);
    }

    private int GetSpawnCount(ItemRule rule)
    {
        int minCount = Mathf.Max(1, rule.itemMinCount);
        int maxCount = Mathf.Max(minCount, rule.itemMaxCount);
        return _prng.Next(minCount, maxCount + 1);
    }

    private async UniTask<List<Vector2Int>> TakePatternPositionsAsync(
        List<Vector2Int> remainingPositions,
        Vector2Int center,
        SpawnShape shape,
        ItemRule rule,
        float patternLength,
        int extraCount)
    {
        if (extraCount <= 0) return new List<Vector2Int>();

        if (_patternStrategies == null || !_patternStrategies.TryGetValue(shape, out IDisposePatternStrategy strategy) || strategy == null)
            return new List<Vector2Int>();

        ObjectRule patternRule = CreatePatternRule(rule, patternLength);
        ObjPatternContext context = new ObjPatternContext(_prng, _ct);
        return await strategy.TakePositionsAsync(context, remainingPositions, center, patternRule, extraCount);
    }

    private ObjectRule CreatePatternRule(ItemRule rule, float patternLength)
    {
        ObjectRule patternRule = new ObjectRule
        {
            patternLength = patternLength,
            curveAngle = rule.curveAngle,
            clusterRadius = rule.clusterRadius
        };

        return patternRule;
    }

    private UniTask<List<Vector2Int>> TakeClusterPositionsAsync(
        List<Vector2Int> remainingPositions,
        Vector2Int center,
        float radius,
        int maxCount)
    {
        return DisposePatternUtils.TakeClusterPositions(_prng, remainingPositions, center, radius, maxCount);
    }

    private void AddPlacement(string prefabKey, Vector2Int tile)
    {
        int instanceId = CreatePlacementId(prefabKey, tile);

        DisposeData placement = new DisposeData
        {
            instanceId = instanceId,
            prefabName = prefabKey,
            tilePosition = tile,
            rotation = Quaternion.Euler(0f, (float)_prng.NextDouble() * 360f, 0f),
            scale = Vector3.one * (0.8f + (float)_prng.NextDouble() * 0.4f),
        };

        _disposeData.DisposeDatas.Add(placement);
        _disposeData.ItemDisposes.Add(placement);
    }

    private Vector2Int TakeRandomPosition(List<Vector2Int> positions)
    {
        int index = _prng.Next(0, positions.Count);
        Vector2Int pos = positions[index];
        positions[index] = positions[positions.Count - 1];
        positions.RemoveAt(positions.Count - 1);
        return pos;
    }

    private int CreatePlacementId(string prefabKey, Vector2Int tile)
    {
        int prefabHash = ComputeStableStringHash(prefabKey);

        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + _worldSettings.WorldSeed;
            hash = (hash * 31) + prefabHash;
            hash = (hash * 31) + tile.x;
            hash = (hash * 31) + tile.y;
            hash = (hash * 31) + _placementSequence++;
            if (hash == 0) hash = 1;
            return hash;
        }
    }

    private int ComputeStableStringHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }
            return hash;
        }
    }
}

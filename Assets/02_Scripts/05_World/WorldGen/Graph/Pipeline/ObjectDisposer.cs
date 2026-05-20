using Cysharp.Threading.Tasks;
using Fusion;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ObjectDisposer : IGraphPipelineStage
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
    private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

    private Dictionary<SpawnShape, IDisposePatternStrategy> _patternStrategies;

    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _disposeSettings = _worldSettings.DisposeSettings;
        _prng = new System.Random(_worldSettings.WorldSeed + (int)WorldSeedChannel.ObjectDisposer);

        _placementSequence = 0;

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

        _stopwatch.Restart();

        foreach (Node node in _nodes)
        {
            _ct.ThrowIfCancellationRequested();

            _currentNode = node;
            await PlaceObjectForNodeAsync();

            if (_stopwatch.ElapsedMilliseconds > 10)
            {
                await UniTask.Yield(_ct);
                _stopwatch.Restart();
            }
        }
    }

    private async UniTask PlaceObjectForNodeAsync()
    {
        PlacementRuleSet ruleSet = _currentNode.RegionData.RegionPlacementRule;
        if (ruleSet == null || ruleSet.ObjectRules == null || ruleSet.ObjectRules.Count == 0)
            return;

            List<Vector2Int> availablePositions = BuildAvailablePositions(_currentNode.CandidatePoints);
        if (availablePositions.Count == 0) return;

        foreach (ObjectRule rule in ruleSet.ObjectRules)
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

    private async UniTask PlacePiecesAsync(ObjectRule rule, string prefabKey, List<Vector2Int> availablePositions)
    {
        if (rule.pieceShapes == null || rule.pieceShapes.Count == 0) return;

        foreach (SpawnShapeDensity entry in rule.pieceShapes)
        {
            _ct.ThrowIfCancellationRequested();
            if (availablePositions.Count == 0) break;

            int remainingQuota = GetTargetCount(availablePositions.Count, entry.density);
            if (remainingQuota <= 0) continue;

            while (remainingQuota > 0 && availablePositions.Count > 0)
            {
                _ct.ThrowIfCancellationRequested();

                Vector2Int center = TakeRandomPosition(availablePositions);
                List<Vector2Int> placements = new List<Vector2Int> { center };

                int extraCount = Mathf.Min(remainingQuota - 1, availablePositions.Count);
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

                remainingQuota -= placements.Count;
            }
        }
    }

    private async UniTask PlaceClustersAsync(ObjectRule rule, string prefabKey, List<Vector2Int> availablePositions)
    {
        if (rule.clusterShapes == null || rule.clusterShapes.Count == 0) return;

        float clusterPatternLength = GetClusterPatternLength(rule);

        foreach (SpawnShapeDensity entry in rule.clusterShapes)
        {
            _ct.ThrowIfCancellationRequested();
            if (availablePositions.Count == 0) break;

            int remainingQuota = GetTargetCount(availablePositions.Count, entry.density);
            if (remainingQuota <= 0) continue;

            while (remainingQuota > 0 && availablePositions.Count > 0)
            {
                _ct.ThrowIfCancellationRequested();

                Vector2Int center = TakeRandomPosition(availablePositions);
                List<Vector2Int> centers = new List<Vector2Int> { center };

                if (entry.shape != SpawnShape.Default)
                {
                    int extraCenterCount = GetClusterPatternCenterCount(rule, clusterPatternLength);
                    extraCenterCount = Mathf.Min(extraCenterCount, availablePositions.Count, remainingQuota - 1);

                    if (extraCenterCount > 0)
                    {
                        List<Vector2Int> extraCenters = await TakePatternPositionsAsync(
                            availablePositions,
                            center,
                            entry.shape,
                            rule,
                            clusterPatternLength,
                            extraCenterCount
                        );
                        centers.AddRange(extraCenters);
                    }
                }

                for (int i = 0; i < centers.Count && remainingQuota > 0; i++)
                {
                    Vector2Int clusterCenter = centers[i];
                    PlaceAndOccupy(prefabKey, clusterCenter, availablePositions);
                    remainingQuota--;

                    if (remainingQuota <= 0 || rule.clusterRadius <= 0f || rule.clusterDensity <= 0f) continue;

                    int clusterCount = GetClusterMaxCount(
                        availablePositions,
                        clusterCenter,
                        rule.clusterRadius,
                        rule.clusterDensity
                    );

                    clusterCount = Mathf.Min(clusterCount, availablePositions.Count, remainingQuota);
                    if (clusterCount <= 0) continue;

                    List<Vector2Int> clusterPositions = await TakeClusterPositionsAsync(
                        availablePositions,
                        clusterCenter,
                        rule.clusterRadius,
                        clusterCount
                    );

                    for (int j = 0; j < clusterPositions.Count && remainingQuota > 0; j++)
                    {
                        PlaceAndOccupy(prefabKey, clusterPositions[j], availablePositions);
                        remainingQuota--;
                    }
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

    private int GetTargetCount(int availableCount, float density)
    {
        if (availableCount <= 0) return 0;

        float clamped = Mathf.Clamp01(density);
        int count = Mathf.RoundToInt(availableCount * clamped);
        return Mathf.Clamp(count, 0, availableCount);
    }

    private int GetClusterMaxCount(
        List<Vector2Int> remainingPositions,
        Vector2Int center,
        float radius,
        float density)
    {
        if (radius <= 0f) return 0;

        float sqrRadius = radius * radius;
        int candidateCount = 0;

        for (int i = 0; i < remainingPositions.Count; i++)
        {
            Vector2Int pos = remainingPositions[i];
            float dx = pos.x - center.x;
            float dy = pos.y - center.y;
            if ((dx * dx) + (dy * dy) <= sqrRadius)
            {
                candidateCount++;
            }
        }

        if (candidateCount <= 0) return 0;

        int maxCount = Mathf.RoundToInt(candidateCount * Mathf.Clamp01(density));
        return Mathf.Clamp(maxCount, 0, candidateCount);
    }

    private float GetClusterPatternLength(ObjectRule rule)
    {
        float length = rule.patternLength + (rule.clusterRadius * 2f);
        return Mathf.Max(1f, length);
    }

    private int GetClusterPatternCenterCount(ObjectRule rule, float patternLength)
    {
        float spacing = Mathf.Max(1f, rule.clusterRadius * 2f);
        int count = Mathf.FloorToInt(patternLength / spacing);
        return Mathf.Max(0, count);
    }

    private ObjectRule CreatePatternRule(ObjectRule sourceRule, float patternLength)
    {
        ObjectRule patternRule = new ObjectRule
        {
            patternLength = patternLength,
            curveAngle = sourceRule.curveAngle,
            clusterRadius = sourceRule.clusterRadius
        };

        return patternRule;
    }

    private async UniTask<List<Vector2Int>> TakePatternPositionsAsync(
        List<Vector2Int> remainingPositions,
        Vector2Int center,
        SpawnShape shape,
        ObjectRule rule,
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
        // 배치 아이디 생성
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
        _disposeData.ObjectDisposes.Add(placement);
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

    private Vector2Int TakeRandomPosition(List<Vector2Int> positions)
    {
        int index = _prng.Next(0, positions.Count);
        Vector2Int pos = positions[index];
        positions[index] = positions[positions.Count - 1];
        positions.RemoveAt(positions.Count - 1);
        return pos;
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

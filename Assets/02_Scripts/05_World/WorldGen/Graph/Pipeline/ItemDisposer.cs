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

    private System.Random _prng;
    private int _placementSequence;
    private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _disposeSettings = _worldSettings.DisposeSettings;
        _prng = new System.Random(_worldSettings.WorldSeed + (int)WorldSeedChannel.ItemDisposer);

    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _disposeData = ctx.DisposeData;
        _logicData = ctx.LogicData;
        _ct = ct;
        List<Node> nodes = ctx.GraphData?.Nodes;

        _placementSequence = ctx.DisposeData.DisposeDatas.Count;

        if (nodes == null) return;

        _stopwatch.Restart();

        foreach (Node node in nodes)
        {
            _ct.ThrowIfCancellationRequested();

            await PlaceItemsForNodeAsync(node);

            if (_stopwatch.ElapsedMilliseconds > 10)
            {
                await UniTask.Yield(_ct);
                _stopwatch.Restart();
            }
        }
    }

    private async UniTask PlaceItemsForNodeAsync(Node node)
    {
        if (node == null || node.BiomeData == null || node.OwnedTiles == null || node.OwnedTiles.Count == 0)
            return;

        DisposeRuleSet ruleSet = node.BiomeData.BiomeDisposeRule;
        if (ruleSet == null || ruleSet.ItemRules == null || ruleSet.ItemRules.Count == 0)
            return;

        foreach (ItemDisposeRule rule in ruleSet.ItemRules)
        {
            _ct.ThrowIfCancellationRequested();

            if (rule == null || string.IsNullOrEmpty(rule.prefabKey))
                continue;

            int maxCount = Mathf.Max(0, rule.maxCount);
            int minCount = Mathf.Clamp(rule.minCount, 0, maxCount);
            int targetCount = maxCount > minCount ? _prng.Next(minCount, maxCount + 1) : maxCount;
            if (targetCount == 0)
                continue;
                
            int oversampleCount = Mathf.Min(node.OwnedTiles.Count, targetCount * 5);
            List<Vector2Int> poissonPoints = await PointSampler.GeneratePoissonPointsAsync(
                node.OwnedTiles,
                oversampleCount,
                _disposeSettings.minObjectDistance,
                _disposeSettings.maxSamplingAttempts,
                _prng,
                _ct);

            int placedCount = 0;
            int occupiedSkips = 0;
            for (int i = 0; i < poissonPoints.Count && placedCount < targetCount; i++)
            {
                _ct.ThrowIfCancellationRequested();

                Vector2Int tile = poissonPoints[i];
                if (!IsOccupied(tile))
                {
                    PlaceAndOccupy(rule.prefabKey, tile);
                    placedCount++;
                }
                else
                {
                    occupiedSkips++;
                }
            }

            Debug.Log($"🌿 [ItemDisposer] '{rule.prefabKey}' 목표={targetCount}, 배치={placedCount}, " +
                      $"점유로스킵={occupiedSkips}, 푸아송점={poissonPoints.Count}, 노드타일={node.OwnedTiles.Count}");
        }
    }


    private bool IsOccupied(Vector2Int pos)
    {
        if (_logicData == null || _logicData.OccupiedWorld == null) return false;
        if (pos.x < 0 || pos.x >= _logicData.TerrainSize.x || pos.y < 0 || pos.y >= _logicData.TerrainSize.y)
            return true;

        return _logicData.OccupiedWorld[pos.x, pos.y];
    }

    private void PlaceAndOccupy(string prefabKey, Vector2Int tile)
    {
        AddPlacement(prefabKey, tile);

        float radius = Mathf.Max(0f, _disposeSettings.minObjectDistance);
        MarkOccupied(tile, radius);
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

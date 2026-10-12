using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>기존 자원·아이템 배치 후 빈 타일에 몬스터 생성 오브젝트를 배치합니다.</summary>
public sealed class SpawnerDisposer : IGraphPipelineStage
{
    private WorldSettings _settings;
    private System.Random _random;
    private readonly Dictionary<string, MonsterSpawnSourceDefinition> _sourceDefinitions = new();

    public void Initialize(WorldSettings settings)
    {
        _settings = settings;
        _random = new System.Random(settings.WorldSeed + 801);
        _sourceDefinitions.Clear();
    }

    public async UniTask ExecuteAsync(WorldGenContext context, CancellationToken token)
    {
        if (context.GraphData?.Nodes == null) return;
        WorldLogicData logic = context.LogicData;
        var usedIds = new HashSet<int>();
        foreach (DisposeData existing in context.DisposeData.DisposeDatas)
            if (existing != null) usedIds.Add(existing.instanceId);

        foreach (Node node in context.GraphData.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if (node?.OwnedTiles == null || node.BiomeData?.BiomeDisposeRule?.SpawnerRules == null) continue;

            foreach (CountDisposeRule rule in node.BiomeData.BiomeDisposeRule.SpawnerRules)
            {
                token.ThrowIfCancellationRequested();
                if (rule == null || string.IsNullOrWhiteSpace(rule.prefabKey)) continue;
                int max = Mathf.Max(0, rule.maxCount);
                int min = Mathf.Clamp(rule.minCount, 0, max);
                if (max == 0) continue;
                MonsterSpawnSourceDefinition definition = await GetSourceDefinitionAsync(rule.prefabKey, token);
                if (definition == null) continue;
                SpawnerPlacementSettings placementSettings = definition.PlacementSettings;
                int count = max > min ? _random.Next(min, max + 1) : max;
                if (count == 0) continue;

                List<Vector2Int> candidates = await PointSampler.GeneratePoissonPointsAsync(
                    node.OwnedTiles, Mathf.Min(node.OwnedTiles.Count, count * 12),
                    Mathf.Max(_settings.DisposeSettings.minObjectDistance, placementSettings.placementRadius * 2f),
                    _settings.DisposeSettings.maxSamplingAttempts, _random, token);

                int placed = 0;
                foreach (Vector2Int tile in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    if (placed >= count) break;
                    if (!CanPlace(logic, tile, placementSettings)) continue;

                    int id = WorldMonsterPlacementId.Create(_settings.WorldSeed, rule.prefabKey, tile, placed, usedIds);
                    var placement = new DisposeData
                    {
                        instanceId = id,
                        prefabName = rule.prefabKey,
                        tilePosition = tile,
                        rotation = Quaternion.Euler(0f, _random.Next(0, 360), 0f),
                        scale = Vector3.one
                    };
                    context.DisposeData.DisposeDatas.Add(placement);
                    context.DisposeData.ObjectDisposes.Add(placement);
                    context.DisposeData.MonsterSpawnSources.Add(new WorldMonsterSpawnSourcePlacement
                    {
                        Placement = placement,
                        Definition = definition
                    });
                    MarkOccupied(logic, tile, placementSettings.placementRadius);
                    placed++;
                }
            }
            await UniTask.Yield(token);
        }
    }

    private async UniTask<MonsterSpawnSourceDefinition> GetSourceDefinitionAsync(string prefabKey, CancellationToken token)
    {
        if (_sourceDefinitions.TryGetValue(prefabKey, out MonsterSpawnSourceDefinition cached)) return cached;

        // 에셋의 설정만 읽습니다. 생성하거나 활성화하지 않아 로컬 청크에 의존하지 않습니다.
        GameObject prefab = await Extensions.LoadAssetAsync<GameObject>(prefabKey, token: token);
        MonsterSpawnSource source = prefab != null ? prefab.GetComponent<MonsterSpawnSource>() : null;
        MonsterSpawnSourceDefinition definition = source != null ? source.GetDefinition() : null;
        _sourceDefinitions[prefabKey] = definition;
        if (definition == null)
            Debug.LogWarning($"[SpawnerDisposer] '{prefabKey}' 프리팹 루트의 MonsterSpawnSource와 Spawn Rule 연결을 확인하세요.");
        return definition;
    }

    private static bool CanPlace(WorldLogicData logic, Vector2Int tile, SpawnerPlacementSettings settings)
    {
        if (logic?.OccupiedWorld == null || logic.HeightWorld == null) return false;
        int radius = Mathf.CeilToInt(Mathf.Max(0f, settings.placementRadius));
        if (tile.x - radius < 0 || tile.y - radius < 0 ||
            tile.x + radius >= logic.TerrainSize.x || tile.y + radius >= logic.TerrainSize.y) return false;
        if (logic.SpawnTile.x >= 0 && (tile - logic.SpawnTile).sqrMagnitude <
            settings.minDistanceFromWorldStart * settings.minDistanceFromWorldStart) return false;

        float centerHeight = logic.HeightWorld[tile.x, tile.y];
        for (int x = tile.x - radius; x <= tile.x + radius; x++)
            for (int y = tile.y - radius; y <= tile.y + radius; y++)
                if (logic.OccupiedWorld[x, y] || logic.TerritoryWorld[x, y] < 0 ||
                    Mathf.Abs(logic.HeightWorld[x, y] - centerHeight) > settings.maxHeightDifference)
                    return false;
        return true;
    }

    private static void MarkOccupied(WorldLogicData logic, Vector2Int tile, float radius)
    {
        int width = Mathf.CeilToInt(Mathf.Max(0f, radius));
        for (int x = tile.x - width; x <= tile.x + width; x++)
            for (int y = tile.y - width; y <= tile.y + width; y++)
                logic.OccupiedWorld[x, y] = true;
    }
}

/// <summary>바이옴별 최초 몬스터 위치를 시드로 확정합니다. 실제 몬스터는 플레이어 접근 시 생성합니다.</summary>
public sealed class InitialMonsterDisposer : IGraphPipelineStage
{
    private WorldSettings _settings;
    private System.Random _random;

    public void Initialize(WorldSettings settings)
    {
        _settings = settings;
        _random = new System.Random(settings.WorldSeed + 901);
    }

    public async UniTask ExecuteAsync(WorldGenContext context, CancellationToken token)
    {
        if (context.GraphData?.Nodes == null) return;
        WorldLogicData logic = context.LogicData;
        var usedIds = new HashSet<int>();
        var placedTiles = new HashSet<Vector2Int>();

        foreach (Node node in context.GraphData.Nodes)
        {
            token.ThrowIfCancellationRequested();
            if (node?.OwnedTiles == null || node.BiomeData?.BiomeSpawnRule?.InitialSpawnRules == null) continue;

            foreach (InitialSpawnRule rule in node.BiomeData.BiomeSpawnRule.InitialSpawnRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.monsterKey)) continue;
                int max = Mathf.Max(0, rule.maxCountPerRegion);
                int min = Mathf.Clamp(rule.minCountPerRegion, 0, max);
                int count = max > min ? _random.Next(min, max + 1) : max;
                if (count == 0) continue;

                List<Vector2Int> candidates = await PointSampler.GeneratePoissonPointsAsync(
                    node.OwnedTiles, Mathf.Min(node.OwnedTiles.Count, count * 10),
                    Mathf.Max(1f, rule.minPlacementDistance), _settings.DisposeSettings.maxSamplingAttempts,
                    _random, token);

                int placed = 0;
                foreach (Vector2Int tile in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    if (placed >= count) break;
                    if (tile.x < 0 || tile.y < 0 || tile.x >= logic.TerrainSize.x || tile.y >= logic.TerrainSize.y ||
                        logic.OccupiedWorld[tile.x, tile.y] || logic.TerritoryWorld[tile.x, tile.y] < 0 ||
                        placedTiles.Contains(tile)) continue;
                    if (logic.SpawnTile.x >= 0 && (tile - logic.SpawnTile).sqrMagnitude <
                        rule.minDistanceFromWorldStart * rule.minDistanceFromWorldStart) continue;

                    int id = WorldMonsterPlacementId.Create(_settings.WorldSeed, rule.monsterKey, tile, placed, usedIds);
                    context.DisposeData.InitialMonsterPlacements.Add(new WorldInitialMonsterPlacement
                    {
                        id = id,
                        monsterKey = rule.monsterKey,
                        tile = tile
                    });
                    placedTiles.Add(tile);
                    placed++;
                }
            }
            await UniTask.Yield(token);
        }
    }
}

internal static class WorldMonsterPlacementId
{
    public static int Create(int seed, string key, Vector2Int tile, int ordinal, HashSet<int> usedIds)
    {
        unchecked
        {
            int hash = seed * 31 + 17;
            foreach (char letter in key) hash = hash * 31 + letter;
            hash = hash * 31 + tile.x;
            hash = hash * 31 + tile.y;
            hash = hash * 31 + ordinal;
            if (hash == 0) hash = 1;
            while (!usedIds.Add(hash)) hash = hash == int.MaxValue ? 1 : hash + 1;
            return hash;
        }
    }
}

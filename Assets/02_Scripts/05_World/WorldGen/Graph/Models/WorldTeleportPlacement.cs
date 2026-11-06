using System;
using System.Collections.Generic;
using UnityEngine;

public enum TeleportCost { None, Hp, Ego, Hunger }

/// <summary>청크 인스턴스와 분리된 연결·비용 정보. 저장/재생성 시에도 유지합니다.</summary>
[Serializable]
public sealed class WorldTeleportPlacement
{
    public int id;
    public int targetId;
    public int regionIndex;
    public bool highlands;
    public Vector2Int tile;
    public Vector2Int arrivalTile;
    public TeleportCost cost;
    public float amount;

    public string PrefabKey => highlands ? "TeleportHighlands" : "TeleportShort";
    public WorldTeleportPlacement Copy() => (WorldTeleportPlacement)MemberwiseClone();
    public Vector3 Position(WorldLogicData logic) => new(tile.x, logic.HeightWorld[tile.x, tile.y], tile.y);
    public Vector3 ArrivalPosition(WorldLogicData logic) =>
        new(arrivalTile.x, logic.HeightWorld[arrivalTile.x, arrivalTile.y] + 0.15f, arrivalTile.y);

    public DisposeData CreateDispose() => new()
    {
        instanceId = id, prefabName = PrefabKey, tilePosition = tile,
        rotation = Quaternion.LookRotation(new Vector3(arrivalTile.x - tile.x, 0f, arrivalTile.y - tile.y)),
        scale = Vector3.one
    };

    /// <summary>출입구와 도착 발판 주변의 자원은 ID를 보존한 채 스폰만 제외합니다.</summary>
    public static bool Contains(WorldLogicData logic, Vector3 position, float margin = 0f)
    {
        if (logic == null) return false;
        float radius = 3f + Mathf.Max(0f, margin);
        foreach (var entry in logic.Teleports)
            if ((new Vector2(position.x, position.z) - (Vector2)entry.tile).sqrMagnitude <= radius * radius ||
                (new Vector2(position.x, position.z) - (Vector2)entry.arrivalTile).sqrMagnitude <= radius * radius)
                return true;
        return false;
    }

    public static bool Intersects(WorldLogicData logic, GameObject instance)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            foreach (var entry in logic.Teleports)
                if (ContainsBounds(renderer.bounds, entry.tile) || ContainsBounds(renderer.bounds, entry.arrivalTile)) return true;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            foreach (var entry in logic.Teleports)
                if (ContainsBounds(collider.bounds, entry.tile) || ContainsBounds(collider.bounds, entry.arrivalTile)) return true;
        return false;
    }

    private static bool ContainsBounds(Bounds bounds, Vector2Int tile)
    {
        float x = Mathf.Clamp(tile.x, bounds.min.x, bounds.max.x) - tile.x;
        float z = Mathf.Clamp(tile.y, bounds.min.z, bounds.max.z) - tile.y;
        return x * x + z * z <= 9f;
    }

    public static void Validate(List<WorldTeleportPlacement> entries, WorldLogicData logic = null)
    {
        if (entries == null) return; // 구버전 저장본에는 목록이 없습니다.
        var ids = new Dictionary<int, WorldTeleportPlacement>();
        var regionCounts = new Dictionary<int, int>();
        int highlandsCount = 0;
        foreach (var entry in entries)
        {
            if (entry == null || entry.id == 0 || entry.targetId == entry.id || !ids.TryAdd(entry.id, entry) ||
                entry.regionIndex < 0 || !Enum.IsDefined(typeof(TeleportCost), entry.cost) ||
                entry.tile.x < 0 || entry.tile.y < 0 || entry.arrivalTile.x < 0 || entry.arrivalTile.y < 0 ||
                float.IsNaN(entry.amount) || float.IsInfinity(entry.amount) || entry.amount < 0f ||
                (entry.highlands ? entry.cost != TeleportCost.None || entry.amount != 0f : entry.cost == TeleportCost.None) ||
                (entry.tile - entry.arrivalTile).sqrMagnitude < 4)
                throw new InvalidOperationException("텔레포트 배치/비용 정보가 올바르지 않습니다.");
            if (entry.highlands) highlandsCount++;
            else
            {
                regionCounts.TryGetValue(entry.regionIndex, out int count);
                if ((regionCounts[entry.regionIndex] = count + 1) > 2)
                    throw new InvalidOperationException("지역별 숏컷 수가 2개를 초과했습니다.");
            }
            if (logic != null && (logic.GetRegionAt(entry.tile.x, entry.tile.y) != entry.regionIndex ||
                logic.GetRegionAt(entry.arrivalTile.x, entry.arrivalTile.y) != entry.regionIndex))
                throw new InvalidOperationException("저장한 텔레포트 위치와 지형이 일치하지 않습니다.");
        }
        if (highlandsCount != 0 && highlandsCount != 2)
            throw new InvalidOperationException("Highlands 텔레포트는 한 쌍이어야 합니다.");
        var pairs = new HashSet<(int, int)>();
        foreach (var entry in entries)
        {
            if (!ids.TryGetValue(entry.targetId, out var target) || target.targetId != entry.id ||
                target.highlands != entry.highlands || target.regionIndex == entry.regionIndex)
                throw new InvalidOperationException("텔레포트 연결 정보가 올바르지 않습니다.");
            if (entry.id < target.id && !pairs.Add((Math.Min(entry.regionIndex, target.regionIndex),
                    Math.Max(entry.regionIndex, target.regionIndex))))
                throw new InvalidOperationException("동일 지역 사이에 중복 텔레포트가 있습니다.");
        }
    }
}

[Serializable]
public sealed class WorldTeleportSettings
{
    [Min(0f)] public float hpCost = 10f;
    [Min(0f)] public float egoCost = 10f;
    [Min(0f)] public float hungerCost = 10f;
    [Range(0f, 60f)] public float maxSlope = 35f;
    [Tooltip("숏컷을 허용할 최소 해안 간격입니다. 갈라지는 초입의 좁은 틈을 제외합니다.")]
    [Min(1f)] public float shortcutMinSeaDistance = 24f;
    [Tooltip("숏컷 양 끝 오브젝트 사이의 최대 거리입니다.")]
    [Min(1f)] public float shortcutMaxDistance = 128f;
    [Tooltip("지역 입구에서 가장 먼 지점까지의 육상 이동 거리 중 이 비율 이상 진입한 해안만 사용합니다.")]
    [Range(0f, 1f)] public float shortcutMinBranchProgress = 0.65f;

    public float Amount(TeleportCost cost) => cost switch
    {
        TeleportCost.Hp => Mathf.Max(0f, hpCost),
        TeleportCost.Ego => Mathf.Max(0f, egoCost),
        TeleportCost.Hunger => Mathf.Max(0f, hungerCost),
        _ => 0f
    };
}

using UnityEngine;

/// <summary>제단과 플레이어 낙하·튕김 연출을 위한 수평 여유 공간을 공통으로 검사합니다.</summary>
public static class WorldSpawnArea
{
    public const float DefaultClearRadius = 6f;

    public static Vector3 GetCenter(WorldLogicData logic)
    {
        if (logic == null) return Vector3.zero;
        Vector2Int tile = logic.SpawnTile;
        if (tile.x < 0 || tile.y < 0 || tile.x >= logic.TerrainSize.x || tile.y >= logic.TerrainSize.y)
            tile = new Vector2Int(logic.TerrainSize.x / 2, logic.TerrainSize.y / 2);
        return new Vector3(tile.x, logic.GetHeightAt(tile.x, tile.y), tile.y);
    }

    public static bool Contains(WorldLogicData logic, Vector2Int tile, float footprintMargin = 0f)
        => Contains(logic, new Vector3(tile.x, 0f, tile.y), footprintMargin);

    public static bool Contains(WorldLogicData logic, Vector3 position, float footprintMargin = 0f)
    {
        if (logic == null) return false;
        Vector3 center = GetCenter(logic);
        float radius = DefaultClearRadius + Mathf.Max(0f, footprintMargin);
        float dx = position.x - center.x;
        float dz = position.z - center.z;
        return dx * dx + dz * dz <= radius * radius;
    }

    public static bool Intersects(WorldLogicData logic, Bounds bounds)
    {
        if (logic == null) return false;
        Vector3 center = GetCenter(logic);
        return Contains(logic, new Vector3(
            Mathf.Clamp(center.x, bounds.min.x, bounds.max.x), 0f,
            Mathf.Clamp(center.z, bounds.min.z, bounds.max.z)));
    }

    public static bool Intersects(WorldLogicData logic, GameObject instance)
    {
        if (logic == null || instance == null) return false;
        if (Contains(logic, instance.transform.position)) return true;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            if (Intersects(logic, renderer.bounds)) return true;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            if (TryGetColliderBounds(collider, out Bounds bounds) && Intersects(logic, bounds)) return true;
        return false;
    }

    public static float GetFootprintMargin(GameObject instance)
    {
        if (instance == null) return 0f;
        float margin = 0f;
        Vector3 origin = instance.transform.position;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            margin = Mathf.Max(margin, GetBoundsMargin(renderer.bounds, origin));
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            if (TryGetColliderBounds(collider, out Bounds bounds))
                margin = Mathf.Max(margin, GetBoundsMargin(bounds, origin));
        return margin;
    }

    private static float GetBoundsMargin(Bounds bounds, Vector3 origin)
        => new Vector2(
            Mathf.Max(Mathf.Abs(bounds.min.x - origin.x), Mathf.Abs(bounds.max.x - origin.x)),
            Mathf.Max(Mathf.Abs(bounds.min.z - origin.z), Mathf.Abs(bounds.max.z - origin.z))).magnitude;

    public static bool IntersectsStructure(WorldLogicData logic, ItemDataSO item, Vector3 position,
        Quaternion rotation, float gridSize = 1f, Vector3? scale = null)
    {
        if (logic == null || item == null) return false;
        Vector3 sizeScale = scale ?? Vector3.one;
        float halfX = Mathf.Max(1, item.placementFootprint.x) * Mathf.Abs(sizeScale.x) * gridSize * 0.5f;
        float halfZ = Mathf.Max(1, item.placementFootprint.y) * Mathf.Abs(sizeScale.z) * gridSize * 0.5f;
        Vector3 center = position + item.placementCheckCenterOffset;
        Vector3 local = Quaternion.Inverse(rotation) * (GetCenter(logic) - center);
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - halfX);
        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - halfZ);
        return dx * dx + dz * dz <= DefaultClearRadius * DefaultClearRadius;
    }

    /// <summary>이전 저장본의 제단 근처 건축물은 상태를 보존한 채 가장 가까운 빈 지면으로 이동합니다.</summary>
    public static bool TryRelocateStructure(WorldLogicData logic, ItemDataSO item, Vector3 original,
        Quaternion rotation, Vector3 scale, out Vector3 position, float footprintMargin = 0f)
    {
        position = original;
        if (logic == null || item == null ||
            (!IntersectsStructure(logic, item, original, rotation, scale: scale) && !Contains(logic, original, footprintMargin)))
            return true;

        float fractionX = original.x - Mathf.Floor(original.x);
        float fractionZ = original.z - Mathf.Floor(original.z);
        float heightOffset = original.y - logic.GetHeightAt(Mathf.FloorToInt(original.x), Mathf.FloorToInt(original.z));
        float bestDistance = float.PositiveInfinity;
        int groundLayer = LayerMask.NameToLayer("Ground");
        int blockingMask = groundLayer >= 0 ? ~(1 << groundLayer) : 0;
        Vector3 half = new Vector3(
            Mathf.Max(1, item.placementFootprint.x) * Mathf.Abs(scale.x) * 0.5f,
            Mathf.Max(0.1f, item.placementCheckHeight * Mathf.Abs(scale.y)) * 0.5f,
            Mathf.Max(1, item.placementFootprint.y) * Mathf.Abs(scale.z) * 0.5f);
        int footprintRadius = Mathf.CeilToInt(new Vector2(half.x, half.z).magnitude);
        Physics.SyncTransforms();
        int originX = Mathf.FloorToInt(original.x);
        int originZ = Mathf.FloorToInt(original.z);
        int maxRing = Mathf.Max(logic.TerrainSize.x, logic.TerrainSize.y);
        for (int ring = 0; ring <= maxRing; ring++)
        {
            // 다음 고리의 최소 거리가 현재 후보보다 멀면 전역 최근접 위치가 확정됩니다.
            if ((float)ring * ring > bestDistance) break;
            for (int offsetX = -ring; offsetX <= ring; offsetX++)
            {
                int stepZ = Mathf.Abs(offsetX) == ring ? 1 : Mathf.Max(1, ring * 2);
                for (int offsetZ = -ring; offsetZ <= ring; offsetZ += stepZ)
                {
                    int x = originX + offsetX;
                    int z = originZ + offsetZ;
                    if (x < footprintRadius || z < footprintRadius ||
                        x >= logic.TerrainSize.x - footprintRadius || z >= logic.TerrainSize.y - footprintRadius) continue;
                    float dx = x + fractionX - original.x;
                    float dz = z + fractionZ - original.z;
                    float distance = dx * dx + dz * dz;
                    if (distance >= bestDistance || logic.GetRegionAt(x, z) < 0) continue;
                    Vector3 candidate = new Vector3(x + fractionX, logic.GetHeightAt(x, z) + heightOffset, z + fractionZ);
                    if (IntersectsStructure(logic, item, candidate, rotation, scale: scale) ||
                        Contains(logic, candidate, footprintMargin)) continue;
                    bool occupied = false;
                    for (int px = x - footprintRadius; px <= x + footprintRadius && !occupied; px++)
                        for (int pz = z - footprintRadius; pz <= z + footprintRadius; pz++)
                            if (logic.GetRegionAt(px, pz) < 0 ||
                                (logic.OccupiedWorld != null && logic.OccupiedWorld[px, pz]))
                            {
                                occupied = true;
                                break;
                            }
                    if (occupied || (blockingMask != 0 && Physics.CheckBox(
                        candidate + item.placementCheckCenterOffset, half, rotation, blockingMask,
                        QueryTriggerInteraction.Ignore))) continue;
                    bestDistance = distance;
                    position = candidate;
                }
            }
        }
        return !float.IsPositiveInfinity(bestDistance);
    }

    private static bool TryGetColliderBounds(Collider collider, out Bounds bounds)
    {
        // Transform 변경 직후에도 물리 동기화 없이 정확한 로컬 형상을 월드 AABB로 변환합니다.
        Bounds local;
        if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
        else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
        else if (collider is CapsuleCollider capsule)
        {
            Vector3 size = Vector3.one * (capsule.radius * 2f);
            size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
            local = new Bounds(capsule.center, size);
        }
        else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
        else
        {
            bounds = collider.bounds;
            return collider.enabled && collider.gameObject.activeInHierarchy;
        }
        Vector3 center = collider.transform.TransformPoint(local.center);
        Vector3 x = collider.transform.TransformVector(new Vector3(local.extents.x, 0f, 0f));
        Vector3 y = collider.transform.TransformVector(new Vector3(0f, local.extents.y, 0f));
        Vector3 z = collider.transform.TransformVector(new Vector3(0f, 0f, local.extents.z));
        bounds = new Bounds(center, new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2f);
        return true;
    }
}

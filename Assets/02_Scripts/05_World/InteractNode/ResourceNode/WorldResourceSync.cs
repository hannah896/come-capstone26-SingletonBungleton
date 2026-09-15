using UnityEngine;

/// <summary>
/// 멀티플레이 월드 공유 상태의 네트워크 구현이 제공하는 기능.
/// 월드/아이템 코드는 이 인터페이스만 보고, Fusion 구현(NetworkWorldState)은 세션 중에만 연결된다.
/// </summary>
public interface IWorldStateNetwork
{
    /// <summary>세션이 살아 있어 요청/조회가 가능한지 여부. (세션 종료 후 해제가 늦어도 싱글 흐름으로 돌아가게)</summary>
    bool IsActive { get; }

    /// <summary>이 피어가 조작하는 캐릭터 (드롭 지급 대상). 없으면 null.</summary>
    GameObject LocalPlayerObject { get; }

    /// <summary>호스트가 합산해 둔 자원 노드의 누적 데미지.</summary>
    int GetResourceDamage(int instanceId);

    /// <summary>자원 노드(또는 월드 배치 아이템)에 데미지를 보고한다. 누적이 maxHealth 이상이면 호스트가 파괴를 확정한다.</summary>
    void ReportResourceDamage(int instanceId, int amount, int maxHealth, float respawnSeconds);

    /// <summary>바닥에 아이템을 떨어뜨리도록 요청한다. (모든 피어에 같은 아이템이 생긴다)</summary>
    void RequestDropItem(string itemKey, int count, Vector3 position);

    /// <summary>바닥 아이템을 줍겠다고 요청한다. 호스트가 승인한 수량만큼 인벤토리에 들어온다.</summary>
    void RequestPickupItem(int dropId, int amount);
}

/// <summary>
/// 월드 자원 노드(나무·돌·풀)와 월드 배치 아이템의 상태를 멀티플레이 피어 간에 맞추는 브리지.
///
/// 월드는 각 피어가 같은 시드로 따로 생성하므로, 배치 오브젝트의 instanceId는 모든 피어에서 같다.
/// 그래서 오브젝트 자체를 네트워크로 보내지 않고 "어떤 instanceId가 얼마나 맞았고, 누가 언제까지 부쉈는지"만 공유한다.
///
/// 흐름 (멀티):
///   1. 타격/줍기 → <see cref="ReportDamage"/> → 호스트가 누적 데미지를 합산
///   2. 누적이 최대 체력에 도달하면 호스트가 파괴를 확정하고 "부순 사람"을 함께 기록
///   3. 모든 피어가 복제된 파괴 테이블을 받아 <see cref="ApplyDestroyed"/>로 자기 월드에 반영
///      - 부순 사람: 드롭/아이템 지급 + 파괴 처리
///      - 나머지: 파괴 이펙트만 보고 제거
///
/// 싱글 플레이(<see cref="IsNetworked"/> == false)에서는 기존처럼 로컬에서 바로 처리한다.
/// </summary>
public static class WorldResourceSync
{
    /// <summary>현재 연결된 네트워크 구현 (세션 중에만 존재).</summary>
    public static IWorldStateNetwork Network { get; private set; }

    /// <summary>월드 상태를 호스트가 관리하는 멀티플레이 중인지 여부.</summary>
    public static bool IsNetworked => Network != null && Network.IsActive;

    /// <summary>원격 파괴를 반영할 수 있을 만큼 월드가 준비됐는지 여부.</summary>
    public static bool IsWorldAvailable => TryGetWorld(out _, out _);

    /// <summary>현재 월드 식별용 참조 (월드가 새로 만들어지면 바뀐다). 준비 전이면 null.</summary>
    public static object CurrentWorld => TryGetWorld(out WorldLogicData logicData, out _) ? logicData : null;

    public static void Bind(IWorldStateNetwork network) => Network = network;

    public static void Unbind(IWorldStateNetwork network)
    {
        if (Network == network) Network = null;
    }

    /// <summary>호스트가 합산해 둔 누적 데미지 (싱글이면 0).</summary>
    public static int GetSharedDamage(int instanceId) => IsNetworked ? Network.GetResourceDamage(instanceId) : 0;

    /// <summary>자원 데미지를 호스트에 보고한다. (멀티 전용)</summary>
    public static void ReportDamage(int instanceId, int amount, int maxHealth, float respawnSeconds)
    {
        if (instanceId == 0 || amount <= 0 || !IsNetworked) return;
        Network.ReportResourceDamage(instanceId, amount, Mathf.Max(1, maxHealth), respawnSeconds);
    }

    /// <summary>
    /// 파괴된 오브젝트가 풀로 돌아가기 직전에 스포너 관리 목록에서 뺀다.
    /// </summary>
    public static void DetachFromSpawner(ChunkData chunk, int instanceId)
    {
        if (chunk == null || instanceId == 0) return;
        if (!TryGetWorld(out _, out WorldObjectSpawner spawner)) return;

        spawner.DetachInstance(chunk.ChunkCoord, instanceId);
    }

    /// <summary>
    /// 호스트가 확정한 파괴를 이 피어의 월드에 반영한다.
    /// destroyedByLocal: 이 피어의 플레이어가 부순 것이면 true (드롭/아이템 지급 대상).
    /// 반환값: 월드가 아직 준비되지 않아 반영하지 못했으면 false (나중에 다시 시도해야 함).
    /// </summary>
    public static bool ApplyDestroyed(int instanceId, float respawnTime, bool destroyedByLocal)
    {
        if (!TryGetWorld(out WorldLogicData logicData, out WorldObjectSpawner spawner)) return false;

        ChunkData chunk = logicData.FindChunkByInstanceId(instanceId);
        if (chunk == null) return true;                        // 이 월드에 없는 ID — 무시

        // 이미 파괴 표시가 있으면 재생성 시각만 호스트 기준으로 맞춘다
        // (다시 생긴 걸 곧바로 또 부순 경우, 이 피어의 재생성 처리가 아직 안 돌았을 수 있다)
        if (chunk.IsObjectDestroyed(instanceId))
        {
            chunk.DestroyedObjects[instanceId] = respawnTime;
            return true;
        }

        chunk.MarkObjectDestroyed(instanceId, respawnTime);

        // 화면에 스폰돼 있지 않으면 청크 표시만으로 충분 (이후 청크 로드 시 스폰되지 않음)
        if (!spawner.TryGetActiveInstance(chunk.ChunkCoord, instanceId, out GameObject instance)) return true;

        GameObject localPlayer = Network?.LocalPlayerObject;

        ResourceNode node = instance.GetComponentInChildren<ResourceNode>(true);
        if (node != null)
        {
            node.ApplyNetworkDestroyed(destroyedByLocal, localPlayer);
            return true;
        }

        Item item = instance.GetComponentInChildren<Item>(true);
        if (item != null)
        {
            WorldItemSync.ApplyPlacedItemTaken(item, destroyedByLocal);
            return true;
        }

        spawner.DetachInstance(chunk.ChunkCoord, instanceId);
        Extensions.Despawn(instance);
        return true;
    }

    private static bool TryGetWorld(out WorldLogicData logicData, out WorldObjectSpawner spawner)
    {
        WorldGenManager manager = WorldGenManager.Instance;
        logicData = manager != null ? manager.CurrentLogicData : null;
        spawner = manager != null && manager.ChunkDirector != null ? manager.ChunkDirector.ObjectSpawner : null;
        return logicData != null && spawner != null;
    }
}

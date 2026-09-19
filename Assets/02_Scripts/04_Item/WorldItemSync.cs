using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 바닥 아이템과 월드 배치 아이템의 생성/줍기를 싱글·멀티 공통으로 처리하는 브리지.
///
/// 멀티(<see cref="WorldResourceSync.IsNetworked"/>):
///   - 드롭: 호스트가 드롭 ID를 발급해 테이블에 올리고, 모든 피어가 같은 아이템을 만든다
///   - 바닥 아이템 줍기: 인벤토리에 들어갈 수량만 요청 → 호스트가 떼어준 만큼 지급 (먼저 요청한 사람이 가져간다)
///   - 월드 배치 아이템 줍기: 자원 파괴와 같은 흐름 (instanceId 파괴 확정 → 부순 사람만 지급)
/// 싱글: 기존처럼 로컬에서 바로 스폰/줍기.
/// </summary>
public static class WorldItemSync
{
    private static int pendingOperations;

    // 이미 시작한 드롭 생성·줍기 지급이 끝난 뒤 저장한다. 입력 차단은 호출자가 담당한다.
    public static async UniTask WaitForPendingAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(10));
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, timeout.Token);
        await UniTask.WaitUntil(() => pendingOperations == 0, cancellationToken: timeout.Token);
    }

    #region 드롭

    /// <summary>
    /// 바닥에 아이템을 떨어뜨린다. 멀티에서는 호스트를 거쳐 모든 피어에 생긴다.
    /// itemKey는 아이템 Addressable 키(= ItemDataSO 이름).
    /// </summary>
    public static void SpawnDroppedItem(string itemKey, int count, Vector3 position, ItemStackSaveData state = null)
    {
        if (string.IsNullOrEmpty(itemKey) || count <= 0) return;

        if (WorldResourceSync.IsNetworked)
        {
            if (state != null)
                NetworkSaveCoordinator.RequestDropWithState(state, position);
            else
                WorldResourceSync.Network.RequestDropItem(itemKey, count, position);
            return;
        }

        SpawnItemAsync(itemKey, count, position, state).Forget();
    }

    /// <summary>
    /// 아이템 오브젝트를 풀에서 꺼내 바닥 아이템 상태로 놓는다. (네트워크 요청 없이 이 피어에만 생성)
    /// </summary>
    public static async UniTask<Item> SpawnItemAsync(string itemKey, int count, Vector3 position, ItemStackSaveData state = null)
    {
        pendingOperations++;
        try { return await SpawnItemCoreAsync(itemKey, count, position, state); }
        finally { pendingOperations--; }
    }

    private static async UniTask<Item> SpawnItemCoreAsync(string itemKey, int count, Vector3 position, ItemStackSaveData state)
    {
        GameObject dropObj;
        try
        {
            dropObj = await Extensions.SpawnAsync(itemKey, null);
        }
        catch (OperationCanceledException)
        {
            return null; // 씬 전환
        }

        if (dropObj == null)
        {
            Debug.LogWarning($"[Drop] SpawnAsync 실패: 주소 '{itemKey}'로 스폰된 오브젝트가 없습니다. Addressables에 등록됐는지 확인하세요.");
            return null;
        }

        Item item = dropObj.GetComponent<Item>();
        if (item == null)
        {
            Debug.LogWarning($"[Drop] '{itemKey}' 프리팹에 Item 컴포넌트가 없습니다.");
            dropObj.transform.position = position;
            return null;
        }

        // 같은 키의 풀을 월드 배치 아이템과 같이 쓰므로 이전 배치 정보를 지운다
        item.ClearWorldState();

        dropObj.transform.position = position;
        if (item.ItemDataSO != null)
            item.Init(item.ItemDataSO);
        item.ResetToWorldTransform();
        SetStackCount(item, count);
        if (state != null) item.RestoreSaveData(state);
        Extensions.GetOrAddComponent<PersistentDroppedItem>(dropObj).Initialize(item);

        return item;
    }

    /// <summary>바닥 아이템의 수량을 호스트 값으로 맞춘다. (누가 일부만 주웠을 때)</summary>
    public static void SetDropCount(Item item, int count)
    {
        if (item == null) return;

        SetStackCount(item, count);
        item.ClearPickupPending();
    }

    public static int GetStackCount(Item item)
    {
        if (item != null && item.itemData is IStackable stackable)
            return Mathf.Max(1, stackable.stackCount);

        return 1;
    }

    private static void SetStackCount(Item item, int count)
    {
        if (item.itemData is IStackable stackable)
            stackable.stackCount = Mathf.Max(1, count);
    }

    #endregion

    #region 줍기

    /// <summary>
    /// 네트워크가 관리하는 아이템이면 줍기 요청을 보내고 true를 반환한다.
    /// false면 로컬 전용 아이템이므로 호출자가 기존 방식으로 줍는다.
    /// </summary>
    public static bool TryRequestPickup(PlayerInventory inventory, Item item)
    {
        if (inventory == null || item == null) return false;

        // 플레이어가 설치한 구조물 아이템 (모닥불 등) — 구조물 디렉터가 관리한다
        if (StructureSync.TryRequestPickup(item)) return true;

        if (!WorldResourceSync.IsNetworked) return false;

        if (item.NetworkDropId != 0)
        {
            if (item.IsPickupPending) return true;

            int addable = inventory.GetAddableAmount(item.ItemDataSO, GetStackCount(item));
            if (addable <= 0) return true; // 인벤토리 가득 — 요청하지 않는다

            item.MarkPickupPending();
            WorldResourceSync.Network.RequestPickupItem(item.NetworkDropId, addable);
            return true;
        }

        if (item.IsWorldPlaced)
        {
            if (item.IsPickupPending) return true;

            // 배치 아이템은 체력 1짜리 자원처럼 파괴를 확정받는다 (동시에 주우면 한 명만 받는다)
            item.MarkPickupPending();
            WorldResourceSync.ReportDamage(item.PlacementId, 1, 1, item.PlacedRespawnSeconds);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 싱글: 다 주운 아이템을 월드에서 치운다. 월드 배치 아이템이면 청크에 파괴 표시를 남겨 재생성 대상으로 만든다.
    /// </summary>
    public static void RemovePickedItem(Item item)
    {
        if (item == null) return;

        if (item.IsWorldPlaced && item.PlacementChunk != null)
        {
            float currentTime = WorldClock.Instance != null ? WorldClock.Instance.TotalInGameSeconds : 0f;
            item.PlacementChunk.MarkObjectDestroyed(item.PlacementId, currentTime + item.PlacedRespawnSeconds);
            WorldResourceSync.DetachFromSpawner(item.PlacementChunk, item.PlacementId);
        }

        item.ClearWorldState();
        Extensions.Despawn(item.gameObject);
    }

    /// <summary>
    /// 멀티: 호스트가 월드 배치 아이템의 줍기를 확정했을 때 (WorldResourceSync.ApplyDestroyed에서 호출).
    /// 주운 사람이면 인벤토리에 넣고, 아니면 제거만 한다.
    /// </summary>
    public static void ApplyPlacedItemTaken(Item item, bool takenByLocal)
    {
        if (item == null) return;

        if (takenByLocal)
        {
            GiveToLocalPlayer(item.ItemDataSO, item.ItemDataSO != null ? item.ItemDataSO.name : null,
                GetStackCount(item), item.transform.position);
        }

        if (item.PlacementChunk != null)
            WorldResourceSync.DetachFromSpawner(item.PlacementChunk, item.PlacementId);

        item.ClearWorldState();
        Extensions.Despawn(item.gameObject);
    }

    /// <summary>멀티: 호스트가 바닥 아이템 줍기를 승인했다. (요청한 피어에서만 호출)</summary>
    public static void HandlePickupGranted(string itemKey, int amount, ItemStackSaveData state = null)
    {
        HandlePickupGrantedAsync(itemKey, amount, state).Forget();
    }

    private static async UniTaskVoid HandlePickupGrantedAsync(string itemKey, int amount, ItemStackSaveData state)
    {
        pendingOperations++;
        try
        {
            ItemDataSO itemSO = await LoadItemDataAsync(itemKey);
            if (itemSO == null) return;

            GameObject player = WorldResourceSync.Network?.LocalPlayerObject;
            Vector3 position = player != null ? player.transform.position : Vector3.zero;
            GiveToLocalPlayer(itemSO, itemKey, amount, position, state);
        }
        finally { pendingOperations--; }
    }

    // 로컬 플레이어 인벤토리에 넣고, 넘치는 수량은 발밑에 다시 떨어뜨린다
    private static void GiveToLocalPlayer(ItemDataSO itemSO, string itemKey, int amount, Vector3 fallbackPosition,
        ItemStackSaveData state = null)
    {
        if (itemSO == null || amount <= 0) return;

        GameObject player = WorldResourceSync.Network?.LocalPlayerObject;
        PlayerInventory inventory = player != null ? player.GetComponentInChildren<PlayerInventory>() : null;

        int remaining = amount;
        if (inventory != null)
            inventory.AddItem(itemSO, amount, out remaining, state?.durability ?? -1f, state?.spoilRemainingSeconds ?? -1f);

        if (remaining > 0)
        {
            Vector3 position = player != null ? player.transform.position : fallbackPosition;
            ItemStackSaveData overflow = state == null ? null : ItemSaveCatalog.Create(itemSO, remaining,
                durability: state.durability, spoilRemainingSeconds: state.spoilRemainingSeconds);
            SpawnDroppedItem(itemKey, remaining, position, overflow);
        }
    }

    #endregion

    #region 데이터

    /// <summary>아이템 키로 ItemDataSO를 찾는다. (프리팹의 Item에 연결된 SO — 프리팹은 리소스 매니저가 캐시한다)</summary>
    public static async UniTask<ItemDataSO> LoadItemDataAsync(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey)) return null;

        GameObject prefab;
        try
        {
            prefab = await Extensions.LoadAssetAsync<GameObject>(itemKey);
        }
        catch (OperationCanceledException)
        {
            return null; // 씬 전환
        }

        Item item = prefab != null ? prefab.GetComponent<Item>() : null;
        if (item == null)
        {
            Debug.LogWarning($"[WorldItemSync] '{itemKey}' 프리팹에서 Item을 찾지 못했습니다.");
            return null;
        }

        return item.ItemDataSO;
    }

    #endregion
}

using UnityEngine;

/// <summary>
/// 구조물 설치/철거/줍기와 보관함·요리솥 조작을 싱글·멀티 공통으로 처리하는 창구.
/// 설치 모드, 망치, 보관함 UI 등 게임 코드는 StorageStation/CookingPot을 직접 조작하지 말고 이 클래스를 호출한다.
///
/// 멀티(<see cref="IsNetworked"/>): 모든 조작을 호스트에 요청하고, 결과는 NetworkStructureDirector 복제로 모든 피어에 반영된다.
///   인벤토리에서 나가는 아이템은 요청 시점에 먼저 빼고, 호스트가 처리하지 못한 만큼은 돌려받는다.
/// 싱글: 로컬에서 바로 처리한다.
/// </summary>
public static class StructureSync
{
    /// <summary>구조물 상태를 호스트가 관리하는 멀티플레이 중인지.</summary>
    public static bool IsNetworked
    {
        get
        {
#if PHOTON_FUSION
            return NetworkStructureDirector.IsActive;
#else
            return false;
#endif
        }
    }

    #region 설치 / 철거 / 줍기

    /// <summary>
    /// 구조물을 설치한다. 호출자는 인벤토리에서 아이템을 이미 뺀 상태여야 한다.
    /// (멀티에서 설치가 거부되면 호스트가 아이템을 돌려준다)
    /// </summary>
    public static void Place(ItemDataSO itemData, Vector3 position, Quaternion rotation)
    {
        if (itemData == null || itemData.placementPrefab == null) return;

#if PHOTON_FUSION
        if (IsNetworked)
        {
            NetworkStructureDirector.Instance.RequestPlace(itemData.name, position, rotation);
            return;
        }
#endif

        GameObject root = Object.Instantiate(itemData.placementPrefab, position, rotation);
        Extensions.GetOrAddComponent<PlacedStructure>(root).ItemKey = itemData.name;
        Extensions.GetOrAddComponent<PersistentStructure>(root).Initialize(itemData);
    }

    /// <summary>망치로 부술 수 있는지. 보관함은 비어 있어야 한다.</summary>
    public static bool CanDemolish(GameObject target)
    {
        if (target == null) return false;

        Structure structure = target.GetComponentInParent<Structure>();
        return structure == null || structure.CanDemolish();
    }

    /// <summary>구조물을 망치로 부순다. 아무것도 돌려받지 않는다.</summary>
    public static void Demolish(GameObject target)
    {
        if (!CanDemolish(target)) return;

#if PHOTON_FUSION
        if (TryGetNetworkId(target, out int id))
        {
            NetworkStructureDirector.Instance.RequestDemolish(id);
            return;
        }
#endif

        Structure structure = target.GetComponentInParent<Structure>();
        if (structure != null)
        {
            structure.Demolish();
            return;
        }

        PlacedStructure placed = target.GetComponentInParent<PlacedStructure>();
        Object.Destroy(placed != null ? placed.gameObject : target);
    }

    /// <summary>
    /// 멀티에서 설치된 구조물 아이템을 줍는 경우 호스트에 요청하고 true를 반환한다.
    /// false면 네트워크 구조물이 아니므로 호출자가 기존 방식으로 줍는다.
    /// </summary>
    public static bool TryRequestPickup(Item item)
    {
#if PHOTON_FUSION
        if (item == null || !TryGetNetworkId(item.gameObject, out int id)) return false;
        if (item.IsPickupPending) return true;

        item.MarkPickupPending();
        NetworkStructureDirector.Instance.RequestPickup(id);
        return true;
#else
        return false;
#endif
    }

    #endregion

    #region 보관함

    /// <summary>
    /// 인벤토리 아이템을 보관함에 넣는다. slotIndex가 음수면 빈 칸/같은 스택을 자동으로 찾는다.
    /// 넣지 못한 수량은 인벤토리로 돌아온다.
    /// </summary>
    public static void Deposit(StorageStation storage, PlayerInventory inventory, ItemDataSO itemData, int amount, int slotIndex = -1)
    {
        if (storage == null || inventory == null || itemData == null || amount <= 0) return;
        if (!inventory.HasItem(itemData, amount)) return;
        if (!storage.CanAccept(itemData)) return;

#if PHOTON_FUSION
        if (TryGetNetworkId(storage.gameObject, out int id))
        {
            inventory.RemoveItem(itemData, amount);
            NetworkStructureDirector.Instance.RequestDeposit(id, slotIndex, itemData.name, amount);
            return;
        }
#endif

        // 남은 소비기한을 먼저 읽어두고(빼고 나면 못 읽는다) 보관함으로 넘긴다
        float remainingSeconds = inventory.GetRemainingExpirationSeconds(itemData);
        inventory.RemoveItem(itemData, amount);

        int remaining;
        if (slotIndex < 0)
            storage.AddItem(itemData, amount, out remaining, remainingSeconds);
        else
            storage.AddItemAt(itemData, slotIndex, amount, out remaining, remainingSeconds);

        if (remaining > 0)
            ReturnToInventory(inventory, itemData, remaining);
    }

    /// <summary>보관함 슬롯에서 amount만큼 꺼내 인벤토리에 넣는다. 인벤토리에 들어갈 만큼만 꺼낸다.</summary>
    public static void Withdraw(StorageStation storage, PlayerInventory inventory, int slotIndex, int amount)
    {
        if (storage == null || inventory == null || amount <= 0) return;
        if (slotIndex < 0 || slotIndex >= storage.SlotCount) return;

        ItemDataSO itemData = storage.Slots[slotIndex];
        if (itemData == null) return;

        int addable = inventory.GetAddableAmount(itemData, Mathf.Min(amount, storage.StackCounts[slotIndex]));
        if (addable <= 0) return;

#if PHOTON_FUSION
        if (TryGetNetworkId(storage.gameObject, out int id))
        {
            NetworkStructureDirector.Instance.RequestWithdraw(id, slotIndex, addable);
            return;
        }
#endif

        // 보관함 배수를 되돌린 기한을 그대로 인벤토리로 넘긴다 (냉장고에서 꺼내면 다시 정상 속도로 상한다)
        float remainingSeconds = storage.GetRemainingSeconds(slotIndex);
        if (storage.RemoveItemAt(slotIndex, addable))
            inventory.AddItem(itemData, addable, out _, remainingSeconds);
    }

    /// <summary>보관함 안에서 슬롯을 교환하거나 같은 아이템이면 합친다.</summary>
    public static void SwapSlots(StorageStation storage, int fromIndex, int toIndex)
    {
        if (storage == null) return;

#if PHOTON_FUSION
        if (TryGetNetworkId(storage.gameObject, out int id))
        {
            NetworkStructureDirector.Instance.RequestSwapSlots(id, fromIndex, toIndex);
            return;
        }
#endif

        storage.SwapOrMergeSlots(fromIndex, toIndex);
    }

    #endregion

    #region 요리솥

    /// <summary>요리를 시작한다. 멀티에서는 요청만 보내고, 실제 시작 여부는 복제된 IsCooking으로 확인한다.</summary>
    public static bool StartCooking(CookingPot pot)
    {
        if (pot == null || pot.IsCooking || !pot.CanCook()) return false;

#if PHOTON_FUSION
        if (TryGetNetworkId(pot.gameObject, out int id))
        {
            NetworkStructureDirector.Instance.RequestCooking(id, true);
            return true;
        }
#endif

        return pot.TryStartCooking();
    }

    public static void StopCooking(CookingPot pot)
    {
        if (pot == null || !pot.IsCooking) return;

#if PHOTON_FUSION
        if (TryGetNetworkId(pot.gameObject, out int id))
        {
            NetworkStructureDirector.Instance.RequestCooking(id, false);
            return;
        }
#endif

        pot.StopCooking();
    }

    #endregion

    #region Helpers

    // 멀티 세션이고 호스트가 발급한 ID가 있는 구조물인지
    private static bool TryGetNetworkId(GameObject target, out int id)
    {
        id = 0;
        if (!IsNetworked || target == null) return false;

        PlacedStructure placed = target.GetComponentInParent<PlacedStructure>();
        id = placed != null ? placed.NetworkId : 0;
        return id != 0;
    }

    // 인벤토리로 돌려주고 넘치는 수량은 발밑에 떨어뜨린다
    private static void ReturnToInventory(PlayerInventory inventory, ItemDataSO itemData, int amount)
    {
        inventory.AddItem(itemData, amount, out int overflow);
        if (overflow > 0)
            WorldItemSync.SpawnDroppedItem(itemData.name, overflow, inventory.transform.position);
    }

    #endregion
}

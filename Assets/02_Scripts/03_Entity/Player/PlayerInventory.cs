using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어가 직접 들고 있는 인벤토리입니다.
/// 플레이어 단위 인벤토리 슬롯과 장착 슬롯을 관리합니다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 20;
    [SerializeField] private int quickSlotCount = 0;

    [Header("줍기 설정")]
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask pickupLayer = ~0;

    [Header("Tool Use")]
    [SerializeField] private float defaultToolUseRange = 2.5f;
    [SerializeField] private LayerMask toolUseLayer = ~0;
    [Tooltip("맨손으로 나무/돌을 칠 때의 데미지 (도구보다 느리게 채집되도록 낮게 유지). 임시값.")]
    [SerializeField] private int bareHandDamage = 1;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();
    [SerializeField] private List<float> expirationTimestamps = new();

    [Header("부패 설정")]
    [Tooltip("expirationTime이 지난 스택이 전환될 아이템 (SpecialType.Rot)")]
    [SerializeField] private ItemDataSO rotItemSO;

    [Header("사망 드롭 설정")]
    [Tooltip("사망 시 소지품을 흩뿌릴 반경 (발밑 기준)")]
    [SerializeField] private float deathDropRadius = 1.5f;

    // 장착 슬롯 드롭 순서 (손 → 몸통 → 머리)
    private static readonly EquipSlot[] EquipDropOrder = { EquipSlot.Hand, EquipSlot.Chest, EquipSlot.Head };

    private readonly Dictionary<EquipSlot, IEquipable> equippedItemInstances = new();
    private readonly Collider[] pickupBuffer = new Collider[16];
    private PlayerInputData inputData;
    private Player owner;
    private int selectedSlotIndex;
    private int hoveredSlotIndex = -1;
    private CharacterController characterController;

    private struct PickupCandidate
    {
        public GameObject GameObject;
        public ItemDataSO ItemData;
        public int Amount;
    }

    // 슬롯을 비운 뒤 스폰을 기다리는 동안 들고 있을 드롭 정보
    private readonly struct PendingDrop
    {
        public readonly ItemDataSO ItemData;
        public readonly int Amount;

        public PendingDrop(ItemDataSO itemData, int amount)
        {
            ItemData = itemData;
            Amount = amount;
        }
    }

    public event Action OnInventoryChanged;
    public event Action<int> OnSelectedSlotChanged;
    public event Action<EquipSlot, ItemDataSO> OnEquippedItemChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;
    public int QuickSlotCount => GetQuickSlotCount();
    public int SelectedSlotIndex => selectedSlotIndex;
    public ItemDataSO EquippedHead { get; private set; }
    public ItemDataSO EquippedChest { get; private set; }
    public ItemDataSO EquippedHand { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        InitializeSlots();
    }

    public void Bind(Player owner, PlayerInputData data)
    {
        this.owner = owner;
        inputData = data;
        InitializeSlots();
    }

    private void OnDestroy()
    {
        ClearEquippedItemInstances();
    }

    public void Tick()
    {
        CheckExpirations();
        TickEquippedTorchDurability();

        if (inputData == null) return;

        if (inputData.QuickSlotIndex >= 0)
            SelectSlot(inputData.QuickSlotIndex);

        if (inputData.QuickSlotScrollDelta != 0)
            MoveSelectedSlot(inputData.QuickSlotScrollDelta);

        if (inputData.PickupPressed)
        {
            if (!TryPickupFocused())
                TryPickupNearest();
        }

        if (inputData.EquipSelectedPressed)
            EquipSelectedSlot();

        if (inputData.ToolUsePressed)
        {
            if (!TryCookAtBonfire() && !TryUseEquippedHandTool())
                TryBareHandHit();
        }

        if (inputData.DropPressed && hoveredSlotIndex >= 0)
            DropFromSlot(hoveredSlotIndex);
    }

    /// <summary>인벤토리 슬롯 UI가 마우스로 가리켜질 때 등록한다 (드롭 단축키 대상 지정용).</summary>
    public void SetHoveredSlot(int slotIndex)
    {
        hoveredSlotIndex = slotIndex;
    }

    /// <summary>가리키던 슬롯에서 마우스가 벗어나면 해제한다. 다른 슬롯이 이미 등록됐다면 무시한다.</summary>
    public void ClearHoveredSlot(int slotIndex)
    {
        if (hoveredSlotIndex == slotIndex)
            hoveredSlotIndex = -1;
    }

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        SelectSlot(Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1));
        OnInventoryChanged?.Invoke();
    }

    public void SetQuickSlotCount(int count)
    {
        quickSlotCount = Mathf.Clamp(count, 0, slotCount);
        SelectSlot(Mathf.Clamp(selectedSlotIndex, 0, GetQuickSlotCount() - 1));
        OnInventoryChanged?.Invoke();
    }

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        return AddItem(itemData, amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount);
        OnInventoryChanged?.Invoke();
        return added;
    }

    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;
        if (!HasItem(itemData, amount)) return false;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == itemData && stackCounts[i] <= 0)
            {
                ClearSlot(i);
                continue;
            }

            if (slots[i] != itemData) continue;

            int removeAmount = Mathf.Min(stackCounts[i], amount);
            stackCounts[i] -= removeAmount;
            amount -= removeAmount;

            if (stackCounts[i] <= 0)
                ClearSlot(i);

            if (amount <= 0)
            {
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public bool HasItem(ItemDataSO itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    public int GetItemCount(ItemDataSO itemData)
    {
        if (itemData == null) return 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] > 0)
                count += stackCounts[i];

        return count;
    }

    /// <summary>슬롯의 Food/Dish 아이템, 또는 회복 수치가 설정된 생자원(열매 등)을
    /// 1개 먹어서 허기/체력/Ego를 회복하고 소모한다.</summary>
    public bool EatFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        if (itemData == null || stackCounts[slotIndex] <= 0) return false;
        if (!IsEdible(itemData)) return false;
        if (owner == null || owner.Stat == null) return false;

        owner.Stat.RestoreHunger(itemData.hungerRestore);
        owner.Stat.RestoreHp(itemData.healthRestore);
        owner.Stat.RestoreEgo(itemData.egoRestore);

        return RemoveItem(itemData, 1);
    }

    /// <summary>Food/Dish는 항상 먹을 수 있고, 자원(Resource)은 회복 수치가 하나라도 설정돼 있으면 생으로 먹을 수 있다.</summary>
    private static bool IsEdible(ItemDataSO itemData)
    {
        if (itemData.itemType == ItemType.Food || itemData.itemType == ItemType.Dish)
            return true;

        return itemData.itemType == ItemType.Resource
            && (itemData.hungerRestore != 0 || itemData.healthRestore != 0 || itemData.egoRestore != 0);
    }

    /// <summary>슬롯의 아이템 전체(스택 통째로)를 플레이어 앞 땅에 드롭한다.</summary>
    public bool DropFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        int amount = stackCounts[slotIndex];
        if (itemData == null || amount <= 0) return false;

        // 스폰 대기 중에 플레이어가 파괴될 수 있으므로 위치를 미리 계산해 둔다.
        Vector3 dropPosition = GetDropOriginPosition() + transform.forward * 1.5f;

        ClearSlot(slotIndex);
        OnInventoryChanged?.Invoke();

        SpawnDroppedItemAsync(itemData, amount, dropPosition).Forget();
        return true;
    }

    /// <summary>
    /// 인벤토리 슬롯과 장착 슬롯의 모든 아이템을 발밑에 흩뿌린다. (사망 시 호출)
    /// </summary>
    /// <returns>드롭된 스택 수</returns>
    public int DropAll()
    {
        List<PendingDrop> pending = new();

        // 장착 아이템은 슬롯으로 되돌리지 않고 곧바로 월드에 떨군다.
        // (인벤토리를 비우는 중이라 되돌릴 공간을 따질 필요가 없다)
        for (int i = 0; i < EquipDropOrder.Length; i++)
        {
            EquipSlot equipSlot = EquipDropOrder[i];
            ItemDataSO equipped = GetEquippedItem(equipSlot);
            if (equipped == null) continue;

            if (ClearEquippedItem(equipSlot))
                pending.Add(new PendingDrop(equipped, 1));
        }

        // 인벤토리 슬롯 전체
        for (int i = 0; i < slots.Count; i++)
        {
            ItemDataSO itemData = slots[i];
            int amount = stackCounts[i];
            if (itemData == null || amount <= 0) continue;

            ClearSlot(i);
            pending.Add(new PendingDrop(itemData, amount));
        }

        if (pending.Count <= 0) return 0;

        OnInventoryChanged?.Invoke();
        ScatterDropsAsync(pending, GetDropOriginPosition()).Forget();
        return pending.Count;
    }

    // 여러 스택을 한 점에 겹쳐 쌓지 않도록 발밑 반경 안에 흩뿌린다.
    private async UniTaskVoid ScatterDropsAsync(List<PendingDrop> drops, Vector3 origin)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * deathDropRadius;
            Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);

            await SpawnDroppedItemAsync(drops[i].ItemData, drops[i].Amount, position);
        }
    }

    private async UniTask SpawnDroppedItemAsync(ItemDataSO itemData, int amount, Vector3 position)
    {
        string address = itemData.name;
        GameObject dropObj = await Extensions.SpawnAsync(address, null);
        if (dropObj == null)
        {
            Debug.LogWarning($"[Drop] SpawnAsync 실패: 주소 '{address}'로 스폰된 오브젝트가 없습니다. Addressables에 등록됐는지 확인하세요.");
            return;
        }

        dropObj.transform.position = position;

        Item item = dropObj.GetComponent<Item>();
        if (item == null)
        {
            Debug.LogWarning($"[Drop] '{address}' 프리팹에 Item 컴포넌트가 없습니다.");
            return;
        }

        item.Init(itemData);
        item.ResetToWorldTransform();
        if (item.itemData is IStackable stackable)
            stackable.stackCount = Mathf.Max(1, amount);
    }

    /// <summary>드롭 아이템이 놓일 기준 지점(플레이어 발밑).</summary>
    private Vector3 GetDropOriginPosition()
    {
        Vector3 feetPosition = transform.position;
        if (characterController != null)
            feetPosition.y += characterController.center.y - characterController.height * 0.5f;

        return feetPosition;
    }

    public bool EquipFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        if (itemData == null || itemData.equipSlot == EquipSlot.None || stackCounts[slotIndex] <= 0)
        {
            if (IsValidSlot(slotIndex) && stackCounts[slotIndex] <= 0)
            {
                ClearSlot(slotIndex);
                OnInventoryChanged?.Invoke();
            }
            return false;
        }

        ItemDataSO currentEquipped = GetEquippedItem(itemData.equipSlot);
        if (currentEquipped == itemData)
        {
            OnInventoryChanged?.Invoke();
            return false;
        }

        // 인벤토리 공간 확인 (실제 제거 전에 먼저 체크)
        if (currentEquipped != null)
        {
            int emptyCount = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null) emptyCount++;

            bool selectedSlotWillBeEmpty = stackCounts[slotIndex] <= 1;
            bool canReturnCurrent = emptyCount > 0 || selectedSlotWillBeEmpty || CanStackItem(currentEquipped);

            if (!canReturnCurrent)
            {
                // 빈 슬롯 없음 + 다른 아이템 → 교체 불가
                OnInventoryChanged?.Invoke();
                return false;
            }
        }

        RemoveOneFromSlot(slotIndex);

        // 기존 장착 아이템을 인벤토리로 돌려보내기
        if (currentEquipped != null)
            TryAddItemToSlots(currentEquipped, 1, out _);

        // 교체 시 기존 아이템 해제 이벤트 명시적으로 발생
        if (currentEquipped != null && currentEquipped != itemData)
            OnEquippedItemChanged?.Invoke(itemData.equipSlot, null); // 해제 알림

        SetEquippedItem(itemData.equipSlot, itemData);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool EquipSelectedSlot()
    {
        return EquipFromSlot(selectedSlotIndex);
    }

    public void SelectSlot(int index)
    {
        if (slots.Count == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, GetQuickSlotCount() - 1);
        if (selectedSlotIndex == clampedIndex) return;

        selectedSlotIndex = clampedIndex;
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
        OnInventoryChanged?.Invoke();
    }

    public void MoveSelectedSlot(int delta)
    {
        if (slots.Count == 0 || delta == 0) return;

        int count = GetQuickSlotCount();
        int nextIndex = (selectedSlotIndex + delta) % count;
        if (nextIndex < 0)
            nextIndex += count;

        SelectSlot(nextIndex);
    }

    public bool EquipItem(ItemDataSO itemData)
    {
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == itemData)
                return EquipFromSlot(i);
        }

        return false;
    }

    public bool UnequipItem(EquipSlot equipSlot)
    {
        if (equipSlot == EquipSlot.None) return false;

        ItemDataSO itemData = GetEquippedItem(equipSlot);
        if (itemData == null) return false;

        // 인벤토리 공간 먼저 확인 (상태 변경 전)
        if (GetEmptySlotIndex() < 0 && !CanStackItem(itemData))
        {
            OnInventoryChanged?.Invoke();
            return false;
        }

        // 공간 확인 후 한 번에 처리
        SetEquippedItem(equipSlot, null);
        TryAddItemToSlots(itemData, 1, out _);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void RegisterEquippedItemInstance(EquipSlot equipSlot, IEquipable equipable)
    {
        if (equipSlot == EquipSlot.None) return;

        UnsubscribeEquippedItemInstance(equipSlot);

        if (equipable == null) return;

        equippedItemInstances[equipSlot] = equipable;
        equipable.OnBroken += OnEquippedItemBroken;
    }

    public void UnregisterEquippedItemInstance(EquipSlot equipSlot, IEquipable equipable)
    {
        if (equipSlot == EquipSlot.None) return;
        if (!equippedItemInstances.TryGetValue(equipSlot, out IEquipable current)) return;
        if (current != equipable) return;

        UnsubscribeEquippedItemInstance(equipSlot);
    }

    public IEquipable GetEquippedItemInstance(EquipSlot equipSlot)
    {
        return equippedItemInstances.TryGetValue(equipSlot, out IEquipable equipable) ? equipable : null;
    }

    // 헬퍼 추가
    private bool CanStackItem(ItemDataSO itemData)
    {
        if (!itemData.isStackable) return false;
        int maxStack = Mathf.Max(1, itemData.maxStack);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] < maxStack)
                return true;
        return false;
    }


    public bool ClearEquippedItem(EquipSlot equipSlot, ItemDataSO expectedItem = null)
    {
        if (equipSlot == EquipSlot.None) return false;

        ItemDataSO itemData = GetEquippedItem(equipSlot);
        if (itemData == null) return false;
        if (expectedItem != null && itemData != expectedItem) return false;

        SetEquippedItem(equipSlot, null);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public ItemDataSO GetEquippedItem(EquipSlot equipSlot) => equipSlot switch
    {
        EquipSlot.Head => EquippedHead,
        EquipSlot.Chest => EquippedChest,
        EquipSlot.Hand => EquippedHand,
        _ => null
    };

    private void InitializeSlots()
    {
        if (slotCount < 1) slotCount = 1;

        // 부족한 슬롯만 뒤에 추가
        while (slots.Count < slotCount)
            slots.Add(null);

        while (stackCounts.Count < slotCount)
            stackCounts.Add(0);

        while (expirationTimestamps.Count < slotCount)
            expirationTimestamps.Add(0f);

        // 초과 슬롯은 뒤에서 제거
        while (slots.Count > slotCount)
            slots.RemoveAt(slots.Count - 1);

        while (stackCounts.Count > slotCount)
            stackCounts.RemoveAt(stackCounts.Count - 1);

        while (expirationTimestamps.Count > slotCount)
            expirationTimestamps.RemoveAt(expirationTimestamps.Count - 1);

        // 기존 데이터 보존: stackCount가 0이면 maxStack 기준으로 복원
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null)
            {
                stackCounts[i] = 0;
                continue;
            }

            // stackCount가 0이면 날리지 말고, 프리펩 설정값으로 보정
            if (stackCounts[i] <= 0)
                stackCounts[i] = 1;  // 최소 1개로 복원
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1);
    }
    private int GetQuickSlotCount()
    {
        if (slots.Count == 0) return 0;
        if (quickSlotCount <= 0) return slots.Count;
        return Mathf.Clamp(quickSlotCount, 1, slots.Count);
    }

    private void FillExistingStacks(ItemDataSO itemData, ref int remainingAmount)
    {
        int maxStack = Mathf.Max(1, itemData.maxStack);

        for (int i = 0; i < slots.Count && remainingAmount > 0; i++)
        {
            if (slots[i] != itemData) continue;
            if (stackCounts[i] <= 0)
            {
                ClearSlot(i);
                continue;
            }

            int space = maxStack - stackCounts[i];
            if (space <= 0) continue;

            int addAmount = Mathf.Min(space, remainingAmount);
            stackCounts[i] += addAmount;
            remainingAmount -= addAmount;

            if (itemData.expirationTime > 0f)
            {
                float newDeadline = Time.time + itemData.expirationTime * 60f;
                expirationTimestamps[i] = expirationTimestamps[i] > 0f
                    ? Mathf.Min(expirationTimestamps[i], newDeadline)
                    : newDeadline;
            }
        }
    }

    private bool TryAddItemToSlots(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        if (itemData.isStackable)
            FillExistingStacks(itemData, ref remainingAmount);

        while (remainingAmount > 0)
        {
            int emptyIndex = GetEmptySlotIndex();
            if (emptyIndex < 0)
                return false;

            int stackSize = itemData.isStackable
                ? Mathf.Min(remainingAmount, Mathf.Max(1, itemData.maxStack))
                : 1;

            slots[emptyIndex] = itemData;
            stackCounts[emptyIndex] = stackSize;
            expirationTimestamps[emptyIndex] = itemData.expirationTime > 0f
                ? Time.time + itemData.expirationTime * 60f
                : 0f;
            remainingAmount -= stackSize;
        }

        return true;
    }

    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                return i;

        return -1;
    }

    private bool IsValidSlot(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    private void RemoveOneFromSlot(int index)
    {
        if (!IsValidSlot(index) || slots[index] == null) return;

        stackCounts[index]--;
        if (stackCounts[index] <= 0)
            ClearSlot(index);
    }

    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
        expirationTimestamps[index] = 0f;
    }

    /// <summary>소비기한이 지난 스택을 rotItemSO로 전환한다.</summary>
    private void CheckExpirations()
    {
        if (rotItemSO == null) return;

        bool anyExpired = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || stackCounts[i] <= 0) continue;
            if (expirationTimestamps[i] <= 0f || Time.time < expirationTimestamps[i]) continue;

            int rotAmount = stackCounts[i];
            ClearSlot(i);
            TryAddItemToSlots(rotItemSO, rotAmount, out _);
            anyExpired = true;
        }

        if (anyExpired)
            OnInventoryChanged?.Invoke();
    }

    /// <summary>손에 든 횃불이 켜져있는 동안 시간 경과에 따라 내구도를 소모시킨다.</summary>
    private void TickEquippedTorchDurability()
    {
        if (EquippedHand == null || EquippedHand.survivalToolType != SurvivalToolType.Torch) return;

        IEquipable torch = GetEquippedItemInstance(EquipSlot.Hand);
        torch?.DrainDurabilityOverTime(Time.deltaTime);
    }

    private void SetEquippedItem(EquipSlot equipSlot, ItemDataSO itemData)
    {
        if (itemData == null)
            UnsubscribeEquippedItemInstance(equipSlot);

        switch (equipSlot)
        {
            case EquipSlot.Head:
                EquippedHead = itemData;
                break;
            case EquipSlot.Chest:
                EquippedChest = itemData;
                break;
            case EquipSlot.Hand:
                EquippedHand = itemData;
                break;
        }

        OnEquippedItemChanged?.Invoke(equipSlot, itemData);
    }

    private void OnEquippedItemBroken(IEquipable brokenItem)
    {
        if (brokenItem == null) return;

        EquipSlot brokenSlot = EquipSlot.None;
        foreach (KeyValuePair<EquipSlot, IEquipable> pair in equippedItemInstances)
        {
            if (pair.Value != brokenItem) continue;

            brokenSlot = pair.Key;
            break;
        }

        if (brokenSlot == EquipSlot.None) return;

        UnsubscribeEquippedItemInstance(brokenSlot);
        ClearEquippedItem(brokenSlot, brokenItem.ItemSO);
    }

    private void UnsubscribeEquippedItemInstance(EquipSlot equipSlot)
    {
        if (!equippedItemInstances.TryGetValue(equipSlot, out IEquipable equipable))
            return;

        equipable.OnBroken -= OnEquippedItemBroken;
        equippedItemInstances.Remove(equipSlot);
    }

    private void ClearEquippedItemInstances()
    {
        if (equippedItemInstances.Count <= 0) return;

        List<EquipSlot> slotsToClear = new(equippedItemInstances.Keys);
        for (int i = 0; i < slotsToClear.Count; i++)
            UnsubscribeEquippedItemInstance(slotsToClear[i]);
    }


    /// <summary>크로스헤어가 조준한 대상이 G키로 주울 수 있는 월드 아이템 / 맨손 채집 노드인지 여부 (UI 포커스 판정용).</summary>
    public bool HasPickupTargetFocused()
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRadius, pickupLayer, QueryTriggerInteraction.Collide))
            return false;

        if (TryGetPickupCandidate(hit.collider, out _))
            return true;

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        return node != null && node.IsHandPickable;
    }

    private bool TryPickupFocused()
    {
        Camera camera = Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, pickupRadius, pickupLayer, QueryTriggerInteraction.Collide))
            return false;

        // 맨손 채집 노드 (풀 등) — G키로 즉시 채집
        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null && node.IsHandPickable)
        {
            node.HandPick(new DamageContext(gameObject, hit.point, 0, "Hand", ActionType.Hand));
            return true;
        }

        if (!TryGetPickupCandidate(hit.collider, out PickupCandidate candidate))
            return false;

        bool added = AddItem(candidate.ItemData, candidate.Amount, out int remainingAmount);
        ApplyPickupResult(candidate, remainingAmount);

        if (added || remainingAmount <= 0)
            Destroy(candidate.GameObject);

        return true;
    }

    private void TryPickupNearest()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            pickupRadius,
            pickupBuffer,
            pickupLayer,
            QueryTriggerInteraction.Collide);

        PickupCandidate nearest = default;
        bool hasNearest = false;
        float nearestSqrDistance = float.MaxValue;
        ResourceNode nearestNode = null;
        float nearestNodeSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = pickupBuffer[i];
            if (col == null) continue;

            if (TryGetPickupCandidate(col, out PickupCandidate candidate))
            {
                float sqrDistance = (candidate.GameObject.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearest = candidate;
                    hasNearest = true;
                    nearestSqrDistance = sqrDistance;
                }
                continue;
            }

            ResourceNode node = col.GetComponentInParent<ResourceNode>();
            if (node != null && node.IsHandPickable)
            {
                float sqrDistance = (node.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < nearestNodeSqrDistance)
                {
                    nearestNode = node;
                    nearestNodeSqrDistance = sqrDistance;
                }
            }
        }

        // 아이템이 더 가깝거나 노드가 없으면 아이템 우선
        if (hasNearest && (nearestNode == null || nearestSqrDistance <= nearestNodeSqrDistance))
        {
            bool added = AddItem(nearest.ItemData, nearest.Amount, out int remainingAmount);
            ApplyPickupResult(nearest, remainingAmount);

            if (added || remainingAmount <= 0)
                Destroy(nearest.GameObject);
            return;
        }

        if (nearestNode != null)
            nearestNode.HandPick(new DamageContext(gameObject, nearestNode.transform.position, 0, "Hand", ActionType.Hand));
    }

    private static bool TryGetPickupCandidate(Collider col, out PickupCandidate candidate)
    {
        Item worldItem = col.GetComponentInParent<Item>();
        if (worldItem != null && worldItem.ItemDataSO != null)
        {
            candidate = new PickupCandidate
            {
                GameObject = worldItem.gameObject,
                ItemData = worldItem.ItemDataSO,
                Amount = GetWorldItemStackCount(worldItem)
            };
            return true;
        }

        candidate = default;
        return false;
    }

    private static void ApplyPickupResult(PickupCandidate candidate, int remainingAmount)
    {
        Item worldItem = candidate.GameObject.GetComponent<Item>();
        if (worldItem != null && worldItem.itemData is IStackable stackable)
            stackable.stackCount = remainingAmount;
    }

    private static int GetWorldItemStackCount(Item worldItem)
    {
        if (worldItem != null && worldItem.itemData is IStackable stackable)
            return Mathf.Max(1, stackable.stackCount);

        return 1;
    }

    /// <summary>
    /// 장착된 도구 타입으로 수행할 ActionType을 반환한다. 유효 타겟이 있을 때만 true.
    /// </summary>
    public bool TryGetToolActionType(out ActionType actionType)
    {
        actionType = ActionType.None;

        ItemDataSO handItem = EquippedHand;
        if (handItem == null || handItem.itemType != ItemType.SurvivalTool)
            return false;

        IEquipable handTool = GetEquippedItemInstance(EquipSlot.Hand);
        if (handTool != null && !handTool.IsUsable)
            return false;

        actionType = GetActionTypeForTool(handItem.survivalToolType);
        if (actionType == ActionType.None)
            return false;

        float range = Mathf.Max(defaultToolUseRange, handItem.attackRange);
        if (!TryRaycastToolTarget(range, out RaycastHit hit))
            return false;

        return TryDamageHitTarget(hit, handItem, actionType, apply: false);
    }

    public void UseEquippedHandTool()
    {
        TryUseEquippedHandTool();
    }

    private static ActionType GetActionTypeForTool(SurvivalToolType toolType) => toolType switch
    {
        SurvivalToolType.Axe_Stone or SurvivalToolType.Axe_Iron or SurvivalToolType.Axe_Gold         => ActionType.Chop,
        SurvivalToolType.Pickaxe_Stone or SurvivalToolType.Pickaxe_Iron or SurvivalToolType.Pickaxe_Gold => ActionType.Mine,
        SurvivalToolType.Shovel_Stone or SurvivalToolType.Shovel_Iron or SurvivalToolType.Shovel_Gold => ActionType.Dig,
        SurvivalToolType.Hammer_Stone or SurvivalToolType.Hammer_Iron or SurvivalToolType.Hammer_Gold => ActionType.Build,
        _ => ActionType.None,
    };

    private bool TryCookAtBonfire()
    {
        if (!TryRaycastToolTarget(defaultToolUseRange + 1f, out RaycastHit hit)) return false;
        BonfireCooker bonfire = hit.collider.GetComponentInParent<BonfireCooker>();
        if (bonfire == null) return false;
        return bonfire.TryCookFromInventory(this);
    }

    /// <summary>손에 든 도구로 채집을 시도한다. 도구가 없으면 false를 반환해 맨손 공격으로 넘긴다.</summary>
    private bool TryUseEquippedHandTool()
    {
        ItemDataSO handItem = EquippedHand;
        if (handItem == null || handItem.itemType != ItemType.SurvivalTool)
            return false;

        IEquipable handTool = GetEquippedItemInstance(EquipSlot.Hand);
        if (handTool != null && !handTool.IsUsable)
            return true;

        float range = Mathf.Max(defaultToolUseRange, handItem.attackRange);

        if (!TryRaycastToolTarget(range, out RaycastHit hit))
            return true;

        ActionType actionType = GetActionTypeForTool(handItem.survivalToolType);
        if (!TryDamageHitTarget(hit, handItem, actionType, apply: true))
            return true;

        handTool?.UseDurability();
        return true;
    }

    /// <summary>도구 없이 맨손으로 나무/돌 자원 노드를 느리게 친다.</summary>
    private void TryBareHandHit()
    {
        if (!TryRaycastToolTarget(defaultToolUseRange, out RaycastHit hit))
            return;

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node == null) return;

        // 맨손 채집은 나무/돌 노드로 한정한다.
        if (node.NodeType != ResourceNodeType.Tree && node.NodeType != ResourceNodeType.Mine)
            return;

        var ctx = new DamageContext(gameObject, hit.point, Mathf.Max(1, bareHandDamage), "Hand", ActionType.Hand);
        if (node.CanDamage(ctx))
            node.ApplyDamage(ctx);
    }

    /// <summary>
    /// 레이캐스트 히트를 데미지 가능한 대상(자원 채집 노드, 몬스터, 망치로 부술 구조물)으로 해석한다.
    /// apply가 true면 실제로 데미지를 적용하고, false면 가능 여부만 확인한다(UI 판정용).
    /// </summary>
    private bool TryDamageHitTarget(RaycastHit hit, ItemDataSO handItem, ActionType actionType, bool apply)
    {
        if (actionType == ActionType.Build)
        {
            Structure structure = hit.collider.GetComponentInParent<Structure>();
            if (structure != null)
            {
                if (!structure.CanDemolish()) return false;
                if (apply) structure.Demolish();
                return true;
            }
            Item placedItem = hit.collider.GetComponentInParent<Item>();
            if (placedItem != null && placedItem.ItemDataSO != null && placedItem.ItemDataSO.itemType == ItemType.Structure)
            {
                if (apply) Destroy(placedItem.gameObject);
                return true;
            }
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(handItem.attackDamage));
        var ctx = new DamageContext(gameObject, hit.point, damage, handItem.itemID, actionType, handItem.harvestableNodeTypes);

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            if (!node.CanDamage(ctx)) return false;
            if (apply) node.ApplyDamage(ctx);
            return true;
        }

        Monster monster = hit.collider.GetComponentInParent<Monster>();
        if (monster != null)
        {
            if (!monster.CanDamage(ctx)) return false;
            if (apply) DamageMonster(monster, ctx, handItem, actionType);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 몬스터에 데미지를 넣는다. 멀티플레이에서는 클라가 직접 깎지 않고 호스트에 보고한다.
    /// (몬스터의 HP/사망 확정은 호스트 전담 — NetworkMonsterDirector 참고)
    /// </summary>
    private void DamageMonster(Monster monster, DamageContext ctx, ItemDataSO handItem, ActionType actionType)
    {
#if PHOTON_FUSION
        var director = NetworkMonsterDirector.Instance;
        if (director != null)
        {
            director.ReportDamage(monster, ctx.Amount, handItem.itemID, actionType);
            return;
        }
#endif
        // 싱글플레이: 디렉터가 없으므로 그대로 로컬 적용
        monster.ApplyDamage(ctx);
    }

    private bool TryRaycastToolTarget(float range, out RaycastHit hit)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Physics.Raycast(ray, out hit, range, toolUseLayer, QueryTriggerInteraction.Collide);
        }

        Vector3 origin = transform.position + Vector3.up;
        return Physics.Raycast(origin, transform.forward, out hit, range, toolUseLayer, QueryTriggerInteraction.Collide);
    }

    private bool IsPointerOverUI()
    {
        if (Main.Instance == null || Main.Input == null || Mouse.current == null)
            return false;

        return Main.Input.IsPointerOverUI(Mouse.current.position.ReadValue());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}

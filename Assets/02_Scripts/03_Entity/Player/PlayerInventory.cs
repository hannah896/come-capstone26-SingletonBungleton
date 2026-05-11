using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어가 직접 들고 있는 인벤토리입니다.
/// 플레이어 단위 인벤토리 슬롯과 장착 슬롯을 관리합니다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 16;

    [Header("줍기 설정")]
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask pickupLayer = ~0;

    [Header("Tool Use")]
    [SerializeField] private float defaultToolUseRange = 2.5f;
    [SerializeField] private LayerMask toolUseLayer = ~0;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();

    private readonly Collider[] pickupBuffer = new Collider[16];
    private PlayerInputData inputData;
    private int selectedSlotIndex;

    private struct PickupCandidate
    {
        public GameObject GameObject;
        public ItemDataSO ItemData;
        public int Amount;
    }

    public event Action OnInventoryChanged;
    public event Action<bool> OnInventoryOpenChanged;
    public event Action<int> OnSelectedSlotChanged;
    public event Action<EquipSlot, ItemDataSO> OnEquippedItemChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;
    public int SelectedSlotIndex => selectedSlotIndex;
    public bool IsOpen { get; private set; }
    public ItemDataSO EquippedHead { get; private set; }
    public ItemDataSO EquippedChest { get; private set; }
    public ItemDataSO EquippedHand { get; private set; }

    private void Awake()
    {
        InitializeSlots();
    }

    public void Bind(Player owner, PlayerInputData data)
    {
        inputData = data;
        InitializeSlots();
    }

    public void Tick()
    {
        if (inputData == null) return;

        ReadFallbackKeyboardInput();

        if (inputData.InventoryTogglePressed)
            Toggle();

        if (inputData.QuickSlotIndex >= 0)
            SelectSlot(inputData.QuickSlotIndex);

        if (inputData.QuickSlotScrollDelta != 0)
            MoveSelectedSlot(inputData.QuickSlotScrollDelta);

        if (inputData.PickupPressed)
            TryPickupNearest();

        if (inputData.EquipSelectedPressed)
            EquipSelectedSlot();

        if (inputData.ToolUsePressed)
            TryUseEquippedHandTool();
    }

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        SelectSlot(Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1));
        OnInventoryChanged?.Invoke();
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        OnInventoryOpenChanged?.Invoke(IsOpen);
    }

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        return AddItem(itemData, amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        if (itemData.isStackable)
            FillExistingStacks(itemData, ref remainingAmount);

        while (remainingAmount > 0)
        {
            int emptyIndex = GetEmptySlotIndex();
            if (emptyIndex < 0)
            {
                OnInventoryChanged?.Invoke();
                return false;
            }

            int stackSize = itemData.isStackable
                ? Mathf.Min(remainingAmount, Mathf.Max(1, itemData.maxStack))
                : 1;

            slots[emptyIndex] = itemData;
            stackCounts[emptyIndex] = stackSize;
            remainingAmount -= stackSize;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;
        if (!HasItem(itemData, amount)) return false;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
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
            if (slots[i] == itemData)
                count += stackCounts[i];

        return count;
    }

    public bool EquipFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        ItemDataSO currentEquipped = GetEquippedItem(itemData.equipSlot);
        RemoveOneFromSlot(slotIndex);

        if (currentEquipped != null && !AddItem(currentEquipped, 1))
        {
            AddItem(itemData, 1);
            return false;
        }

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

        int clampedIndex = Mathf.Clamp(index, 0, slots.Count - 1);
        if (selectedSlotIndex == clampedIndex) return;

        selectedSlotIndex = clampedIndex;
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
        OnInventoryChanged?.Invoke();
    }

    public void MoveSelectedSlot(int delta)
    {
        if (slots.Count == 0 || delta == 0) return;

        int count = slots.Count;
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

        SetEquippedItem(equipSlot, null);

        if (!AddItem(itemData, 1))
        {
            SetEquippedItem(equipSlot, itemData);
            OnInventoryChanged?.Invoke();
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
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

        while (slots.Count < slotCount)
            slots.Add(null);

        while (stackCounts.Count < slotCount)
            stackCounts.Add(0);

        while (slots.Count > slotCount)
            slots.RemoveAt(slots.Count - 1);

        while (stackCounts.Count > slotCount)
            stackCounts.RemoveAt(stackCounts.Count - 1);

        for (int i = 0; i < slotCount; i++)
            if (slots[i] == null)
                stackCounts[i] = 0;

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1);
    }

    private void FillExistingStacks(ItemDataSO itemData, ref int remainingAmount)
    {
        int maxStack = Mathf.Max(1, itemData.maxStack);

        for (int i = 0; i < slots.Count && remainingAmount > 0; i++)
        {
            if (slots[i] != itemData) continue;

            int space = maxStack - stackCounts[i];
            if (space <= 0) continue;

            int addAmount = Mathf.Min(space, remainingAmount);
            stackCounts[i] += addAmount;
            remainingAmount -= addAmount;
        }
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
    }

    private void SetEquippedItem(EquipSlot equipSlot, ItemDataSO itemData)
    {
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

    private void ReadFallbackKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.tabKey.wasPressedThisFrame)
            inputData.InventoryTogglePressed = true;

        if (keyboard.gKey.wasPressedThisFrame)
            inputData.PickupPressed = true;

        if (keyboard.eKey.wasPressedThisFrame)
            inputData.EquipSelectedPressed = true;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame && !IsPointerOverUI())
                inputData.ToolUsePressed = true;

            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0f)
                inputData.QuickSlotScrollDelta = -1;
            else if (scrollY < 0f)
                inputData.QuickSlotScrollDelta = 1;
        }

        for (int i = 0; i < 9; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);
            if (!keyboard[key].wasPressedThisFrame) continue;

            inputData.QuickSlotIndex = i;
            break;
        }
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

        for (int i = 0; i < count; i++)
        {
            Collider col = pickupBuffer[i];
            if (col == null) continue;

            if (!TryGetPickupCandidate(col, out PickupCandidate candidate))
                continue;

            float sqrDistance = (candidate.GameObject.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearest = candidate;
            hasNearest = true;
            nearestSqrDistance = sqrDistance;
        }

        if (!hasNearest) return;

        bool added = AddItem(nearest.ItemData, nearest.Amount, out int remainingAmount);
        ApplyPickupResult(nearest, remainingAmount);

        if (added || remainingAmount <= 0)
            Destroy(nearest.GameObject);
    }

    private static bool TryGetPickupCandidate(Collider col, out PickupCandidate candidate)
    {
        DroppedItem droppedItem = col.GetComponentInParent<DroppedItem>();
        if (droppedItem != null && droppedItem.itemData != null)
        {
            candidate = new PickupCandidate
            {
                GameObject = droppedItem.gameObject,
                ItemData = droppedItem.itemData,
                Amount = Mathf.Max(1, droppedItem.amount)
            };
            return true;
        }

        Item item = col.GetComponentInParent<Item>();
        if (item != null && item.itemData != null)
        {
            candidate = new PickupCandidate
            {
                GameObject = item.gameObject,
                ItemData = item.itemData,
                Amount = Mathf.Max(1, item.stackCount)
            };
            return true;
        }

        candidate = default;
        return false;
    }

    private static void ApplyPickupResult(PickupCandidate candidate, int remainingAmount)
    {
        DroppedItem droppedItem = candidate.GameObject.GetComponent<DroppedItem>();
        if (droppedItem != null)
        {
            droppedItem.amount = remainingAmount;
            return;
        }

        Item item = candidate.GameObject.GetComponent<Item>();
        if (item != null)
            item.stackCount = remainingAmount;
    }

    private void TryUseEquippedHandTool()
    {
        ItemDataSO handItem = EquippedHand;
        if (handItem == null || handItem.itemType != ItemType.SurvivalTool)
            return;

        SurvivalToolType toolType = handItem.survivalToolType;
        float range = Mathf.Max(defaultToolUseRange, handItem.attackRange);

        if (!TryRaycastToolTarget(range, out RaycastHit hit))
            return;

        GatherableObject gatherable = hit.collider.GetComponentInParent<GatherableObject>();
        if (gatherable == null)
            return;

        gatherable.OnHit(toolType);
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

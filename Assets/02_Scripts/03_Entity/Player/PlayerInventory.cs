using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어가 직접 들고 있는 인벤토리입니다.
/// 아이템 시스템의 전역 InventoryManager와 분리해서 플레이어 단위 슬롯만 관리합니다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 16;

    [Header("줍기 설정")]
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask pickupLayer = ~0;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();

    private readonly Collider[] pickupBuffer = new Collider[16];
    private PlayerInputData inputData;

    public event Action OnInventoryChanged;
    public event Action<bool> OnInventoryOpenChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;
    public bool IsOpen { get; private set; }

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

        if (inputData.InteractPressed)
            TryPickupNearest();
    }

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
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

    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
    }

    private void ReadFallbackKeyboardInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.tabKey.wasPressedThisFrame)
            inputData.InventoryTogglePressed = true;

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

        DroppedItem nearest = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = pickupBuffer[i];
            if (col == null) continue;

            DroppedItem droppedItem = col.GetComponentInParent<DroppedItem>();
            if (droppedItem == null || droppedItem.itemData == null) continue;

            float sqrDistance = (droppedItem.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearest = droppedItem;
            nearestSqrDistance = sqrDistance;
        }

        if (nearest == null) return;

        bool added = AddItem(nearest.itemData, nearest.amount, out int remainingAmount);
        nearest.amount = remainingAmount;

        if (added || remainingAmount <= 0)
            Destroy(nearest.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}

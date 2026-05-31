using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 보관 기능을 할 수 있는 구조물들의 부모 클래스(상자, 냉장고, 요리솥 등)
/// </summary>
public abstract class StorageStation : StationBase
{
    [Header("보관 설정")]
    [SerializeField] private int slotCount = 16;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();

    public event Action OnStorageChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;

    protected override void Awake()
    {
        base.Awake();
        InitializeSlots();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        InitializeSlots();
    }

    public override void Interact(InteractionContext context)
    {
        OnInteract(context);
    }

    protected virtual void OnInteract(InteractionContext context) { }

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        return AddItem(itemData, amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount);
        OnStorageChanged?.Invoke();
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
                OnStorageChanged?.Invoke();
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

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        OnStorageChanged?.Invoke();
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
        {
            if (slots[i] == null)
            {
                stackCounts[i] = 0;
                continue;
            }

            if (stackCounts[i] <= 0)
                stackCounts[i] = 1;
        }
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

    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
    }
}

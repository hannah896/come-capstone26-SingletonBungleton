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
    [SerializeField] private List<int> stackCounts = new();     // 각 슬롯에 있는 아이템의 개수 (0이면 빈 슬롯)

    // 보관함 내용이 변경될 때마다 호출되는 이벤트 (UI 등에서 구독하여 업데이트에 활용)
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

    /// <summary>보관함은 비어있을 때만 부술 수 있다.</summary>
    public override bool CanDemolish()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && stackCounts[i] > 0)
                return false;

        return true;
    }

    protected virtual void OnInteract(InteractionContext context) { }

    // 상자에 아이템을 추가하는 메서드, 남은 개수 반환, 성공 여부 반환
    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount);
        OnStorageChanged?.Invoke();
        return added;
    }
    public bool AddItemAt(ItemDataSO itemData,  int index,  int amount = 1)
    {
        return AddItemAt(itemData, index,  amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItemAt(ItemDataSO itemData, int index, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (index < 0 || index >= slots.Count) return false;
        if (itemData == null || amount <= 0) return false;
        if (slots[index] != null && slots[index] != itemData)
            return false; // 다른 아이템이 있는 슬롯에는 추가 불가
        int maxStack = Mathf.Max(1, itemData.maxStack);
        int currentCount = stackCounts[index];
        int space = maxStack - currentCount;
        if (space <= 0)
            return false; // 스택이 이미 가득 찬 경우
        int addAmount = Mathf.Min(space, remainingAmount);
        slots[index] = itemData;
        stackCounts[index] += addAmount;
        remainingAmount -= addAmount;
        OnStorageChanged?.Invoke();
        return true;
    }

    // 슬롯 간 아이템 교환 또는 같은 아이템이면 스택 합치기, 인덱스 유효성 검사 포함
    public void SwapOrMergeSlots(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= slotCount || toIndex < 0 || toIndex >= slotCount) return;
        ItemDataSO tempItem = slots[fromIndex];
        int tempCount = stackCounts[fromIndex];
        if (slots[fromIndex] == slots[toIndex] && tempItem != null)
        {
            // 같은 아이템이면 합치기
            int maxStack = Mathf.Max(1, tempItem.maxStack);
            int total = tempCount + stackCounts[toIndex];
            stackCounts[toIndex] = Mathf.Min(total, maxStack);
            stackCounts[fromIndex] = total - stackCounts[toIndex];
            if (stackCounts[fromIndex] <= 0)
                ClearSlot(fromIndex);
        }
        else
        {
            // 다른 아이템이면 교환
            slots[fromIndex] = slots[toIndex];
            stackCounts[fromIndex] = stackCounts[toIndex];
            slots[toIndex] = tempItem;
            stackCounts[toIndex] = tempCount;
        }
        OnStorageChanged?.Invoke();
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

    // 슬롯 인덱스를 지정하여 특정 개수 만큼 아이템 제거, 성공 여부 반환
    public bool RemoveItemAt(int index, int amount = 1)
    {
        if (index < 0 || index >= slots.Count) return false;
        if (slots[index] == null || stackCounts[index] < amount) return false;

        stackCounts[index] -= amount;
        if (stackCounts[index] <= 0)
        {
            ClearSlot(index);
        }

        OnStorageChanged?.Invoke();
        return true;
    }

    // 특정 아이템이 보관되어 있는지, 요청된 개수 이상으로 보관되어 있는지 여부 반환
    public bool HasItem(ItemDataSO itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    // 보관된 아이템 개수를 반환, 아이템이 없거나 null이면 0
    public int GetItemCount(ItemDataSO itemData)
    {
        if (itemData == null) return 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] > 0)
                count += stackCounts[i];

        return count;
    }

    // 슬롯 개수 변경 메서드, 기존 데이터 유지 또는 초기화 후 이벤트 호출
    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        OnStorageChanged?.Invoke();
    }

    // slotCount 변경 시점과 초기화 시점에 슬롯 리스트와
    // 스택 카운트 리스트의 크기를 slotCount에 맞게 조정하고, 기존 데이터 유지 또는 초기화
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

    // 이미 보관된 아이템과 같은 종류의 아이템이 있다면 먼저 그 스택을 채우는 시도,
    // 상자가 꽉 찬 경우 남은 개수 반환
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

    // 가장 먼저 비어있는 슬롯부터 아이템을 추가하는 시도, 성공 여부와 남은 개수 반환
    private bool TryAddItemToSlots(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        if (itemData.isStackable)
            FillExistingStacks(itemData, ref remainingAmount);

        // remainingAmount : 요청이 들어온 아이템 개수 중 아직 보관되지 않은 개수
        while (remainingAmount > 0)
        {
            // 빈 슬롯 찾기 (없으면 실패)
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

    // 비어있는 슬롯의 인덱스를 반환, 없으면 -1
    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                return i;

        return -1;
    }

    // 슬롯 초기화 또는 아이템이 제거되어 빈 슬롯이 된 경우 호출
    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
    }
}

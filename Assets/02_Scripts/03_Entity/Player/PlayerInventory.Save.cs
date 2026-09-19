using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class PlayerInventory
{
    // 장비 모델은 인벤토리가 소유한다. 뷰를 없애거나 다시 생성해도 내구도는 유지된다.
    private readonly List<float> slotDurabilities = new();
    private readonly Dictionary<EquipSlot, float> equippedSpoilDeadlines = new();

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount,
        float durability, float spoilRemainingSeconds)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount, durability, spoilRemainingSeconds);
        OnInventoryChanged?.Invoke();
        return added;
    }

    public ItemStackSaveData CaptureSlot(int index)
    {
        if (!IsValidSlot(index) || slots[index] == null || stackCounts[index] <= 0) return null;
        return ItemSaveCatalog.Create(slots[index], stackCounts[index], index, slotDurabilities[index],
            expirationTimestamps[index] <= 0f ? -1f : Mathf.Max(0f, expirationTimestamps[index] - SavePlayClock.Now));
    }

    public void SetSlotState(int index, float durability, float spoilRemainingSeconds)
    {
        if (!IsValidSlot(index) || slots[index] == null) return;
        slotDurabilities[index] = slots[index].hasDurability
            ? Mathf.Clamp(durability < 0f ? slots[index].maxDurability : durability, 0f, slots[index].maxDurability) : -1f;
        expirationTimestamps[index] = spoilRemainingSeconds < 0f ? 0f : Mathf.Max(float.Epsilon, SavePlayClock.Now + spoilRemainingSeconds);
    }

    private ItemStackSaveData CaptureEquipment(EquipSlot slot)
    {
        var so = GetEquippedItem(slot);
        if (so == null) return null;
        var runtime = GetEquippedItemInstance(slot) as ItemData_Equipable;
        float deadline = equippedSpoilDeadlines.TryGetValue(slot, out var value) ? value : -1f;
        return ItemSaveCatalog.Create(so, 1, -1, runtime?.CurrentDurability ?? -1f,
            deadline < 0f ? -1f : Mathf.Max(0f, deadline - SavePlayClock.Now));
    }

    public InventorySaveData CaptureSaveData()
    {
        var saved = new InventorySaveData
        {
            slotCount = slotCount, quickSlotCount = quickSlotCount, selectedSlotIndex = selectedSlotIndex
        };
        for (int i = 0; i < slots.Count; i++)
        {
            var stack = CaptureSlot(i);
            if (stack != null) saved.slots.Add(stack);
        }
        foreach (var slot in EquipDropOrder)
        {
            var item = CaptureEquipment(slot);
            if (item != null) saved.equipment.Add(new EquipmentSaveData { equipSlot = slot, item = item });
        }
        return saved;
    }

    public async UniTask RestoreSaveDataAsync(InventorySaveData saved, CancellationToken token = default)
    {
        if (saved == null || saved.slotCount < 1 || saved.slotCount > 10000)
            throw new InvalidOperationException("저장된 인벤토리 크기가 올바르지 않습니다.");
        var resolvedSlots = new Dictionary<int, ItemDataSO>();
        var resolvedEquipment = new Dictionary<EquipSlot, ItemDataSO>();
        foreach (var stack in saved.slots ?? new List<ItemStackSaveData>())
        {
            if (stack == null || stack.slotIndex < 0 || stack.slotIndex >= saved.slotCount
                || stack.count <= 0 || resolvedSlots.ContainsKey(stack.slotIndex))
                throw new InvalidOperationException("저장된 인벤토리 슬롯이 올바르지 않습니다.");
            var so = await ItemSaveCatalog.ResolveAsync(stack, token);
            if (stack.count > (so.isStackable ? Mathf.Max(1, so.maxStack) : 1))
                throw new InvalidOperationException($"아이템 '{stack.itemId}'의 저장 수량이 스택 제한을 초과합니다.");
            resolvedSlots.Add(stack.slotIndex, so);
        }
        foreach (var equipment in saved.equipment ?? new List<EquipmentSaveData>())
        {
            if (equipment == null || equipment.equipSlot == EquipSlot.None || equipment.item == null
                || resolvedEquipment.ContainsKey(equipment.equipSlot))
                throw new InvalidOperationException("저장된 장비 슬롯이 올바르지 않습니다.");
            var so = await ItemSaveCatalog.ResolveAsync(equipment.item, token);
            if (so == null || so.equipSlot != equipment.equipSlot || equipment.item.count != 1)
                throw new InvalidOperationException("저장된 장비 종류와 장착 위치가 일치하지 않습니다.");
            resolvedEquipment.Add(equipment.equipSlot, so);
        }
        token.ThrowIfCancellationRequested();

        // 모든 에셋의 확인이 끝난 다음 한 번에 교체한다. AddItem으로 슬롯을 재배치하지 않는다.
        foreach (var slot in EquipDropOrder) SetEquippedItem(slot, null);
        slotCount = saved.slotCount;
        quickSlotCount = Mathf.Clamp(saved.quickSlotCount, 0, slotCount);
        InitializeSlots();
        for (int i = 0; i < slotCount; i++) ClearSlot(i);
        foreach (var stack in saved.slots ?? new List<ItemStackSaveData>())
        {
            slots[stack.slotIndex] = resolvedSlots[stack.slotIndex];
            stackCounts[stack.slotIndex] = stack.count;
            SetSlotState(stack.slotIndex, stack.durability, stack.spoilRemainingSeconds);
        }
        foreach (var equipment in saved.equipment ?? new List<EquipmentSaveData>())
            SetEquippedItem(equipment.equipSlot, resolvedEquipment[equipment.equipSlot],
                equipment.item.durability, equipment.item.spoilRemainingSeconds);
        selectedSlotIndex = Mathf.Clamp(saved.selectedSlotIndex, 0, GetQuickSlotCount() - 1);
        hoveredSlotIndex = -1;
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
        OnInventoryChanged?.Invoke();
    }
}

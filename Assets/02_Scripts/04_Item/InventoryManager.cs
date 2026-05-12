using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 15;
    [SerializeField] private float foodFreshnessDecayRate = 0.001f;

    // 일반 슬롯 (null = 빈 슬롯)
    public List<ItemInstance> slots = new List<ItemInstance>();

    // 장착 슬롯
    public ItemInstance equippedHead;
    public ItemInstance equippedChest;
    public ItemInstance equippedHand;

    public event System.Action OnInventoryChanged;
    public event System.Action<ItemDataSO, int> OnInventoryFull;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        //DontDestroyOnLoad(gameObject);

        for (int i = 0; i < slotCount; i++)
            slots.Add(null);
    }

    private void Update()
    {
        bool changed = false;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (slot.data == null) continue;
            if (slot.data.itemType != ItemType.Food) continue;
            if (slot.freshness <= 0f) continue;

            slot.freshness = Mathf.Max(0f, slot.freshness - foodFreshnessDecayRate * Time.deltaTime);
            if (slot.freshness <= 0f)
            {
                Debug.Log($"[음식] {slot.data.itemName}이(가) 상했습니다!");
                changed = true;
            }
        }
        if (changed) OnInventoryChanged?.Invoke();
    }

    // ── 아이템 추가 ───

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null) return false;
        return AddItem(new ItemInstance(itemData, amount));
    }

    // 인스턴스를 그대로 넣기 — 내구도/신선도 보존 (바닥 아이템 줍기 등)
    public bool AddItem(ItemInstance incoming)
    {
        if (incoming == null || incoming.data == null) return false;

        int amount = incoming.stackCount;

        // 스택 가능이면 기존 슬롯에 먼저 쌓기
        if (incoming.data.isStackable)
        {
            for (int i = 0; i < slots.Count && amount > 0; i++)
            {
                if (slots[i] == null || slots[i].data != incoming.data) continue;
                amount = slots[i].AddStack(amount);
            }
        }

        // 남은 수량을 빈 슬롯에 넣기
        while (amount > 0)
        {
            int emptyIdx = GetEmptySlotIndex();
            if (emptyIdx == -1)
            {
                OnInventoryChanged?.Invoke();
                OnInventoryFull?.Invoke(incoming.data, amount);
                return false;
            }

            int stackSize = incoming.data.isStackable
                ? Mathf.Min(amount, incoming.data.maxStack)
                : 1;

            var newInst = new ItemInstance(incoming.data, stackSize);
            newInst.currentDurability = incoming.currentDurability;
            newInst.freshness = incoming.freshness;

            slots[emptyIdx] = newInst;
            amount -= stackSize;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    // ── 아이템 제거 ───

    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == null || slots[i].data != itemData) continue;

            if (slots[i].stackCount >= amount)
            {
                slots[i].RemoveStack(amount);
                if (slots[i].stackCount == 0) ClearSlot(i);
                OnInventoryChanged?.Invoke();
                return true;
            }
            else
            {
                amount -= slots[i].stackCount;
                ClearSlot(i);
            }
        }
        return false;
    }

    // ── 장착 ───

    public bool EquipItem(ItemDataSO itemData)
    {
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        // 인벤토리에서 해당 아이템 찾기
        int foundIdx = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].data == itemData) { foundIdx = i; break; }
        }
        if (foundIdx == -1) return false;

        ItemInstance toEquip = slots[foundIdx];
        ClearSlot(foundIdx);

        ItemInstance current = GetEquippedItem(itemData.equipSlot);
        if (current != null) AddItem(current);

        SetEquippedItem(itemData.equipSlot, toEquip);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 장착 해제 → 인벤토리로 반환
    public void UnequipSlot(EquipSlot slot)
    {
        ItemInstance inst = GetEquippedItem(slot);
        SetEquippedItem(slot, null);
        if (inst != null) AddItem(inst);
        OnInventoryChanged?.Invoke();
    }

    // 장착 해제 → 버리기 (아이템 파괴 시 사용)
    public void UnequipAndDiscard(EquipSlot slot)
    {
        SetEquippedItem(slot, null);
        OnInventoryChanged?.Invoke();
    }

    // ── 도구 합치기 ───

    public bool CombineTools(int slotA, int slotB)
    {
        if (slots[slotA] == null || slots[slotB] == null) return false;
        if (slots[slotA].data != slots[slotB].data) return false;
        if (!slots[slotA].data.hasDurability) return false;

        float combined = slots[slotA].currentDurability + slots[slotB].currentDurability;
        slots[slotA].currentDurability = Mathf.Min(combined, slots[slotA].data.maxDurability);
        Debug.Log($"[합치기] {slots[slotA].data.itemName} 내구도 → {slots[slotA].currentDurability:F0}/{slots[slotA].data.maxDurability:F0}");
        ClearSlot(slotB);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ── 음식 먹기 ───

    public bool EatItem(ItemDataSO foodData)
    {
        ItemInstance slot = null;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].data == foodData) { slot = slots[i]; break; }
        }
        if (slot == null) return false;

        if (slot.IsExpired())
        {
            Debug.Log($"[음식] {foodData.itemName}이(가) 상해서 먹을 수 없습니다!");
            // TODO: 디버프 적용
            return false;
        }

        Debug.Log($"[음식] {foodData.itemName} 섭취!");
        // TODO: SurvivalManager.Instance.AddHunger(foodData.hungerRestore) 등
        if (foodData.hungerRestore > 0) Debug.Log($"배고픔 +{foodData.hungerRestore}");
        if (foodData.healthRestore > 0) Debug.Log($"체력 +{foodData.healthRestore}");
        if (foodData.sanityRestore > 0) Debug.Log($"정신력 +{foodData.sanityRestore}");

        RemoveItem(foodData, 1);
        return true;
    }

    // ── 조회 ───

    public int GetItemCount(ItemDataSO itemData)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].data == itemData) total += slots[i].stackCount;
        return total;
    }

    public bool HasItem(ItemDataSO itemData, int amount = 1)
        => GetItemCount(itemData) >= amount;

    public ItemInstance GetEquippedItem(EquipSlot slot) => slot switch
    {
        EquipSlot.Head => equippedHead,
        EquipSlot.Chest => equippedChest,
        EquipSlot.Hand => equippedHand,
        _ => null
    };

    // ── 내부 헬퍼 ───
    
    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null) return i;
        return -1;
    }

    private void ClearSlot(int index) => slots[index] = null;

    private void SetEquippedItem(EquipSlot slot, ItemInstance inst)
    {
        switch (slot)
        {
            case EquipSlot.Head:  equippedHead  = inst; break;
            case EquipSlot.Chest: equippedChest = inst; break;
            case EquipSlot.Hand:  equippedHand  = inst; break;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 15;

    public List<ItemDataSO> slots = new();
    public List<int> stackCounts = new();

    public ItemDataSO equippedHead;
    public ItemDataSO equippedChest;
    public ItemDataSO equippedHand;

    public event System.Action OnInventoryChanged;
    public event System.Action<ItemDataSO, int> OnInventoryFull;

    public int SlotCount => slotCount;

    public static InventoryManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        InventoryManager manager = FindFirstObjectByType<InventoryManager>();
        if (manager != null)
        {
            Instance = manager;
            manager.InitializeSlots();
            return Instance;
        }

        var go = new GameObject("@InventoryManager");
        return go.AddComponent<InventoryManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeSlots();
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

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        return AddItem(itemData, amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        if (itemData.isStackable)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != itemData) continue;

                int space = itemData.maxStack - stackCounts[i];
                if (space <= 0) continue;

                int add = Mathf.Min(space, remainingAmount);
                stackCounts[i] += add;
                remainingAmount -= add;

                if (remainingAmount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        while (remainingAmount > 0)
        {
            int emptyIdx = GetEmptySlotIndex();
            if (emptyIdx == -1)
            {
                Debug.Log("[인벤토리] 가득 참. 남은 아이템은 바닥에 드랍합니다.");
                OnInventoryChanged?.Invoke();
                OnInventoryFull?.Invoke(itemData, remainingAmount);
                return false;
            }

            int stackSize = itemData.isStackable
                ? Mathf.Min(remainingAmount, itemData.maxStack)
                : 1;

            slots[emptyIdx] = itemData;
            stackCounts[emptyIdx] = stackSize;
            remainingAmount -= stackSize;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] != itemData) continue;

            if (stackCounts[i] >= amount)
            {
                stackCounts[i] -= amount;
                if (stackCounts[i] == 0) ClearSlot(i);
                OnInventoryChanged?.Invoke();
                return true;
            }

            amount -= stackCounts[i];
            ClearSlot(i);
        }

        OnInventoryChanged?.Invoke();
        return false;
    }

    public bool EquipItem(ItemDataSO itemData)
    {
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        ItemDataSO current = GetEquippedItem(itemData.equipSlot);
        if (current != null) AddItem(current, 1);

        SetEquippedItem(itemData.equipSlot, itemData);
        RemoveItem(itemData, 1);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool CombineTools(int slotA, int slotB)
    {
        if (!IsValidSlot(slotA) || !IsValidSlot(slotB)) return false;
        if (slots[slotA] == null || slots[slotB] == null) return false;
        if (slots[slotA] != slots[slotB]) return false;
        if (!slots[slotA].hasDurability) return false;

        Debug.Log($"[합치기] {slots[slotA].itemName} 내구도 회복");
        ClearSlot(slotB);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetItemCount(ItemDataSO itemData)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData)
                total += stackCounts[i];

        return total;
    }

    public bool HasItem(ItemDataSO itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
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

    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
    }

    private ItemDataSO GetEquippedItem(EquipSlot slot) => slot switch
    {
        EquipSlot.Head => equippedHead,
        EquipSlot.Chest => equippedChest,
        EquipSlot.Hand => equippedHand,
        _ => null
    };

    private void SetEquippedItem(EquipSlot slot, ItemDataSO data)
    {
        switch (slot)
        {
            case EquipSlot.Head:
                equippedHead = data;
                break;
            case EquipSlot.Chest:
                equippedChest = data;
                break;
            case EquipSlot.Hand:
                equippedHand = data;
                break;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 15; // 기본 15칸

    // 일반 슬롯 (itemData와 stackCount를 같은 인덱스로 관리)
    public List<ItemDataSO> slots = new List<ItemDataSO>();
    public List<int> stackCounts = new List<int>();

    // 장착 슬롯 (머리/가슴/손 3칸)
    public ItemDataSO equippedHead;
    public ItemDataSO equippedChest;
    public ItemDataSO equippedHand;

    // UI가 이 이벤트를 구독해서 자동으로 갱신
    public event System.Action OnInventoryChanged;
    public event System.Action<ItemDataSO, int> OnInventoryFull;


    private void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 슬롯 초기화
        for (int i = 0; i < slotCount; i++)
        {
            slots.Add(null);
            stackCounts.Add(0);
        }
    }

    // 인벤토리에 아이템 추가
    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null) return false;

        if (itemData.isStackable)  // 스택 가능 아이템이면 기존 슬롯에 먼저 쌓기
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != itemData) continue;
                int space = itemData.maxStack - stackCounts[i];
                if (space <= 0) continue;

                int add = Mathf.Min(space, amount);
                stackCounts[i] += add;
                amount -= add;

                if (amount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // 빈 슬롯에 추가
        while (amount > 0)
        {
            int emptyIdx = GetEmptySlotIndex();
            if (emptyIdx == -1)
            {
                Debug.Log("[인벤토리] 가득 참! 바닥에 드랍합니다.");
                OnInventoryFull?.Invoke(itemData, amount);
                return false;
            }

            int stackSize = itemData.isStackable
                ? Mathf.Min(amount, itemData.maxStack)
                : 1;

            slots[emptyIdx] = itemData;
            stackCounts[emptyIdx] = stackSize;
            amount -= stackSize;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }
    
    // 인벤토리에서 아이템 제거
    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
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
            else
            {
                amount -= stackCounts[i];
                ClearSlot(i);
            }
        }
        return false;
    }

    // 아이템 장착 (인벤토리 슬롯 우클릭 시 호출)
    public bool EquipItem(ItemDataSO itemData)
    {
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        // 기존 장착 아이템 인벤토리로 반환
        ItemDataSO current = GetEquippedItem(itemData.equipSlot);
        if (current != null) AddItem(current, 1);

        SetEquippedItem(itemData.equipSlot, itemData);
        RemoveItem(itemData, 1);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 도구 합치기 (같은 도구 2개 합쳐 내구도 회복)
    public bool CombineTools(int slotA, int slotB)
    {
        if (slots[slotA] == null || slots[slotB] == null) return false;
        if (slots[slotA] != slots[slotB]) return false;
        if (!slots[slotA].hasDurability) return false;

        Debug.Log($"[합치기] {slots[slotA].itemName} 내구도 회복!");
        ClearSlot(slotB);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 헬퍼 메서드
    public int GetItemCount(ItemDataSO itemData)
    {
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData) total += stackCounts[i];
        return total;
    }

    public bool HasItem(ItemDataSO itemData, int amount = 1)
        => GetItemCount(itemData) >= amount;

    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null) return i;
        return -1;
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
            case EquipSlot.Head: equippedHead = data; break;
            case EquipSlot.Chest: equippedChest = data; break;
            case EquipSlot.Hand: equippedHand = data; break;
        }
    }
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 모닥불 요리 컴포넌트.
/// - 인벤토리 요리: 선택 슬롯 아이템을 우클릭으로 요리 (RemoveItem → AddItem)
/// - 월드 드롭 요리: 아이템이 트리거 안에 들어오면 구운 버전으로 교체
/// </summary>
public class BonfireCooker : MonoBehaviour
{
    [System.Serializable]
    public struct CookingEntry
    {
        public ItemDataSO rawItem;
        public ItemDataSO cookedItem;
        public string cookedPrefabKey;
    }

    [SerializeField] private List<CookingEntry> cookingTable = new();

    // ── 인벤토리 요리 (우클릭) ──────────────────────────────────
    public bool TryCookFromInventory(PlayerInventory inventory)
    {
        if (inventory == null) return false;

        int idx = inventory.SelectedSlotIndex;
        var slots = inventory.Slots;
        if (idx < 0 || idx >= slots.Count || slots[idx] == null) return false;

        ItemDataSO selected = slots[idx];
        foreach (var entry in cookingTable)
        {
            if (entry.rawItem == null || entry.cookedItem == null) continue;
            if (selected != entry.rawItem) continue;

            inventory.RemoveItem(entry.rawItem, 1);
            inventory.AddItem(entry.cookedItem, 1);
            Debug.Log($"[모닥불] {entry.rawItem.itemName} → {entry.cookedItem.itemName}");
            return true;
        }
        return false;
    }

    // ── 월드 드롭 요리 (트리거) ─────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        Item item = other.GetComponentInParent<Item>();
        if (item == null) return;

        foreach (var entry in cookingTable)
        {
            if (entry.rawItem == null || string.IsNullOrEmpty(entry.cookedPrefabKey)) continue;
            if (item.ItemDataSO != entry.rawItem) continue;
            CookDropAsync(item, entry.cookedPrefabKey).Forget();
            return;
        }
    }

    private async UniTaskVoid CookDropAsync(Item item, string cookedKey)
    {
        Vector3 spawnPos = item.transform.position;
        Destroy(item.gameObject);

        GameObject cooked = await Extensions.SpawnAsync(cookedKey, null);
        if (cooked != null)
            cooked.transform.position = spawnPos;
    }
}

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
        if (!TryFindInventoryEntry(inventory, out CookingEntry entry)) return false;

        inventory.RemoveItem(entry.rawItem, 1);
        inventory.AddItem(entry.cookedItem, 1);
        Debug.Log($"[모닥불] {entry.rawItem.itemName} → {entry.cookedItem.itemName}");
        return true;
    }

    /// <summary>선택 슬롯의 아이템을 이 모닥불에서 구울 수 있는지 (크로스헤어 포커스 판정용).</summary>
    public bool CanCookFromInventory(PlayerInventory inventory)
        => TryFindInventoryEntry(inventory, out _);

    private bool TryFindInventoryEntry(PlayerInventory inventory, out CookingEntry found)
    {
        found = default;
        if (inventory == null) return false;

        int idx = inventory.SelectedSlotIndex;
        var slots = inventory.Slots;
        if (idx < 0 || idx >= slots.Count || slots[idx] == null) return false;

        ItemDataSO selected = slots[idx];
        foreach (var entry in cookingTable)
        {
            if (entry.rawItem == null || entry.cookedItem == null) continue;
            if (selected != entry.rawItem) continue;

            found = entry;
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

            if (WorldResourceSync.IsNetworked)
                CookNetworkDrop(item, entry.cookedPrefabKey);
            else
                CookDropAsync(item, entry.cookedPrefabKey).Forget();
            return;
        }
    }

    // 멀티: 트리거는 모든 피어에서 발생하므로 호스트만 판정한다.
    // 호스트가 날것 드롭을 지우고 구운 드롭을 올리면 복제로 모든 피어에서 교체된다.
    private static void CookNetworkDrop(Item item, string cookedKey)
    {
        if (Main.Network == null || !Main.Network.IsHost) return;
        if (item.NetworkDropId == 0) return; // 네트워크 드롭이 아닌 로컬 전용 아이템

        int count = WorldItemSync.GetStackCount(item);
        Vector3 position = item.transform.position;

        WorldResourceSync.Network.RemoveDrop(item.NetworkDropId);
        WorldItemSync.SpawnDroppedItem(cookedKey, count, position);
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

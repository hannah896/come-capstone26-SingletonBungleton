using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    public ItemInstance itemInstance;

    // GatherableObject에서 스폰할 때 호출 (상태 포함 전달)
    public void Setup(ItemInstance instance)
    {
        itemInstance = instance;
    }

    // 편의 오버로드: 새 인스턴스 생성 (기본 내구도/신선도)
    public void Setup(ItemDataSO data, int amount)
    {
        itemInstance = new ItemInstance(data, amount);
    }

    // PlayerPickup에서 호출 — 내구도/신선도 보존
    public void Pickup()
    {
        bool success = InventoryManager.Instance.AddItem(itemInstance);
        if (success)
            Destroy(gameObject);
        else
            Debug.Log("[줍기] 인벤토리 가득 참!");
    }
}

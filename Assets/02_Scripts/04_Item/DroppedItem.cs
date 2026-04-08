using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    public ItemDataSO itemData;
    public int amount = 1;

    // GatherableObject에서 스폰할 때 호출
    public void Setup(ItemDataSO data, int amt)
    {
        itemData = data;
        amount = amt;
    }

    // PlayerPickup에서 호출
    public void Pickup()
    {
        bool success = InventoryManager.Instance.AddItem(itemData, amount);
        if (success)
            Destroy(gameObject);
        else
            Debug.Log("[줍기] 인벤토리 가득 참!");
    }
}

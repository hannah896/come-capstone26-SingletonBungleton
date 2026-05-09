using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    public ItemDataSO itemData;
    public int amount = 1;

    public void Setup(ItemDataSO data, int amt)
    {
        itemData = data;
        amount = Mathf.Max(1, amt);
    }

    public void Pickup()
    {
        InventoryManager.EnsureInstance();

        bool success = InventoryManager.Instance.AddItem(itemData, amount, out int remainingAmount);
        amount = remainingAmount;

        if (success || amount <= 0)
            Destroy(gameObject);
        else
            Debug.Log("[以띻린] ?몃깽?좊━ 媛??李?");
    }
}

using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    public ItemDataSO itemData;
    public int amount = 1;

    public void Setup(ItemDataSO data, int amt)
    {
        itemData = data;
        amount = Mathf.Max(1, amt);
        SetKinematic(false);
    }

    public void Pickup()
    {
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("[DroppedItem] 아이템을 받을 PlayerInventory를 찾지 못했습니다.");
            return;
        }

        bool success = inventory.AddItem(itemData, amount, out int remainingAmount);
        amount = remainingAmount;

        if (success || amount <= 0)
            Destroy(gameObject);
        else
            Debug.Log("[以띻린] ?몃깽?좊━ 媛??李?");
    }

    private void SetKinematic(bool isKinematic)
    {
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].isKinematic = isKinematic;
    }
}

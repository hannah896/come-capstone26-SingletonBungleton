using UnityEngine;

public class Item_Food : Item
{
    [Header("=== 음식 전용 ===")]
    public FoodType foodType = FoodType.None;

    protected override void Init()
    {
        base.Init();
        if (itemData != null)
            foodType = itemData.foodType;
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        foodType = itemData.foodType;
    }

    // 인벤토리 UI에서 음식 사용 시 InventoryManager.EatItem()을 직접 호출하세요.
    // 이 메서드는 월드 오브젝트(손에 든 음식 등)에서 직접 먹을 때 사용합니다.
    public void Eat()
    {
        //if (InventoryManager.Instance != null)
        //{
        //    InventoryManager.Instance.EatItem(itemData);
        //}
        //else
        //{
        //    // 인벤토리 없이 직접 먹는 경우 (테스트 등)
        //    Debug.Log($"[음식] {itemData.itemName} 섭취");
        //    if (itemData.hungerRestore > 0) Debug.Log($"배고픔 +{itemData.hungerRestore}");
        //    if (itemData.healthRestore > 0) Debug.Log($"체력 +{itemData.healthRestore}");
        //    if (itemData.sanityRestore > 0) Debug.Log($"정신력 +{itemData.sanityRestore}");
        //}
    }

    public override string ToString()
    {
        return base.ToString() + $" [FoodType:{foodType}]";
    }
}

using UnityEngine;

/// <summary>
/// 자원 아이템 클래스
/// 나무: 숯, 장작, 나뭇가지, 열매
/// 광물: 금, 돌, 부싯돌
/// 풀
/// </summary>


public class Item_Resource : Item
{
    [Header("=== 자원 전용 속성 ===")]
    [Tooltip("자원 세부 타입")]
    public ResourceType resourceType = ResourceType.None;

    protected override void Init()
    {
        base.Init();
        if (itemData == null) return;

        resourceType = itemData.resourceType;

        if (!itemData.isStackable)
            Debug.LogWarning($"[자원] {itemData.itemName}: 스택 불가 설정 확인 필요!");
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        resourceType = itemData.resourceType;
    }


    //자원 사용(크래프팅 재료)
    public void UseAsIngredient(int amount)
    {
        int removed = RemoveStack(amount);
        Debug.Log($"[자원] {itemData.itemName} {removed}개 소모됨 (남은 수량: {stackCount})");

        if (stackCount <= 0)
            Destroy(gameObject);
    }
}
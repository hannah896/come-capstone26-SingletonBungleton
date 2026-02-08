using UnityEngine;

/// <summary>
/// 자원 아이템 클래스
/// 나무: 숯, 장작, 나뭇가지, 열매
/// 광물: 금, 돌, 부싯돌
/// 잡초
/// </summary>


public class Item_Resource : Item
{
    [Header("=== 자원 전용 속성 ===")]
    [Tooltip("자원 세부 타입")]
    public ResourceType resourceType = ResourceType.None;

    protected override void Init()
    {
        base.Init();

        // 자원은 기본적으로 겹치기 가능해야 함
        if (!itemData.isStackable)
        {
            Debug.LogWarning($"{itemData.itemName}은 자원인데 겹치기가 불가능합니다!");
        }
    }

    protected override void Init(ItemDataSO data)
    {
        base.Init(data);
    }


//자원 사용(크래프팅 재료)
    public void UseAsIngredient(int amount)
    {
        int removed = RemoveStack(amount);
        Debug.Log($"{itemData.itemName} {removed}개 사용됨");

        if (stackCount <= 0)
        {
            Destroy(gameObject);
        }
    }


// UI
    public override string ToString()
    {
        return base.ToString();
    }
}
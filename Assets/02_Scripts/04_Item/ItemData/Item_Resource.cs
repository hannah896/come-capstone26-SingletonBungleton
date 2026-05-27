using UnityEngine;

/// <summary>
/// 자원 런타임 데이터 클래스
/// - 나무(숯, 장작, 나뭇가지, 열매), 광물(금, 돌, 부싯돌, 철, 석탄), 풀
/// </summary>
public class Item_Resource : ItemData, IStackable
{
    public ResourceType resourceType;

    public int stackCount { get; set; } = 1;
    public int stackMax => data.maxStack;

    public bool CanStackWith(ItemDataSO otherSO) => stackCount < stackMax && otherSO == data;

    public Item_Resource(ItemDataSO data, int count = 1) : base(data)
    {
        resourceType = data.resourceType;
        stackCount = count;

        if (!data.isStackable)
            Debug.LogWarning($"[자원] {data.itemName}: 스택 불가 설정 확인 필요!");
    }

    public void UseAsIngredient(int amount)
    {
        IStackable stack = this;
        int removed = stack.RemoveStack(amount);
        Debug.Log($"[자원] {data.itemName} {removed}개 소모됨 (남은 수량: {stackCount})");
    }
}

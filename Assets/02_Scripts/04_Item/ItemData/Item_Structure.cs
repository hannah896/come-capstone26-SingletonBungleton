using UnityEngine;

/// <summary>
/// 구조물(설치형) 아이템 런타임 데이터 클래스. 인벤토리에 머무르지 않고 제작 즉시 설치 모드로 넘어간다 (PlacementController).
/// </summary>
public class Item_Structure : ItemData, IStackable
{
    public int stackCount { get; set; } = 1;
    public int stackMax => data.maxStack;
    public bool CanStackWith(ItemDataSO otherSO) => stackCount < stackMax && otherSO == data;

    public Item_Structure(ItemDataSO data, int count = 1) : base(data)
    {
        stackCount = Mathf.Max(1, count);
    }
}

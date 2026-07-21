using UnityEngine;

/// <summary>
/// 특수 아이템(부패물 등) 런타임 데이터 클래스.
/// </summary>
public class Item_Special : ItemData, IStackable
{
    public SpecialType specialType;

    public int stackCount { get; set; } = 1;
    public int stackMax => data.maxStack;
    public bool CanStackWith(ItemDataSO otherSO) => stackCount < stackMax && otherSO == data;

    public Item_Special(ItemDataSO data, int count = 1) : base(data)
    {
        specialType = data.specialType;
        stackCount = Mathf.Max(1, count);
    }
}

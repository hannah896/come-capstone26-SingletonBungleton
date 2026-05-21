using UnityEngine;

/// <summary>
/// ResourceNode에서 얻어진 아이템 자원 데이터 클래스
/// </summary>
public class ItemData_ResourceItem : ItemData, IStackable
{
    public int stackMax => data.maxStack;

    public new int stackCount { get; set; } = 1;

    public ItemData_ResourceItem(ItemDataSO data, int count = 1) : base(data, count)
    {
        stackCount = count > 0 ? count : 1;
    }
}

using UnityEngine;

/// <summary>
/// 요리(Dish) 런타임 데이터 클래스.
/// hungerRestore / healthRestore / egoRestore 는 SO에서 읽는다.
/// </summary>
[System.Serializable]
public class ItemData_Dish : ItemData, IStackable
{
    public DishType id;

    public int stackCount { get; set; } = 1;
    public int stackMax => data.maxStack;
    public bool CanStackWith(ItemDataSO otherSO) => stackCount < stackMax && otherSO == data;

    public ItemData_Dish(ItemDataSO data, int count = 1) : base(data)
    {
        id = data.dishType;
        stackCount = Mathf.Max(1, count);
    }
}

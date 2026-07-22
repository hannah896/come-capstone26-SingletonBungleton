/// <summary>
/// 런타임 아이템 데이터의 추상 베이스 클래스.
/// ItemDataSO(정적 설정) + 런타임 상태(내구도, 스택 등)를 함께 관리.
/// </summary>
[System.Serializable]
public abstract class ItemData
{
    public ItemDataSO data;

    public ItemData(ItemDataSO data)
    {
        this.data = data;
    }

    /// <summary>
    /// SO의 ItemType에 맞는 런타임 데이터 객체를 생성한다.
    /// Item.Awake()와 드롭 시스템에서 사용.
    /// </summary>
    public static ItemData CreateFromSO(ItemDataSO so, int count = 1)
    {
        if (so == null) return null;

        return so.itemType switch
        {
            ItemType.Resource     => new Item_Resource(so, count),
            ItemType.CombatGear   => new Item_CombatGear(so),
            ItemType.SurvivalTool => new Item_SurvivalTool(so),
            ItemType.Booty        => new Item_Booty(so, count),
            ItemType.Food         => new ItemData_Food(so, count),
            ItemType.Dish         => new ItemData_Dish(so, count),
            ItemType.Special      => new Item_Special(so, count),
            _                     => LogUnknownType(so)
        };
    }

    private static ItemData LogUnknownType(ItemDataSO so)
    {
        UnityEngine.Debug.LogError($"[ItemData] 알 수 없는 ItemType: {so.itemType} ({so.itemName})");
        return null;
    }
}

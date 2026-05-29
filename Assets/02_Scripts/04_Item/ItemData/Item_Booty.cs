using UnityEngine;

/// <summary>
/// 전리품 런타임 데이터 클래스
/// - 생고기, 가죽, 깃털, 뿔 등
/// </summary>
public class Item_Booty : ItemData, IStackable
{
    public BootyType bootyType;
    public string sourceMonsterName;

    public int stackCount { get; set; }
    public int stackMax => data.maxStack;

    public bool CanStackWith(ItemDataSO otherSO) => stackCount < stackMax && otherSO == data;

    public Item_Booty(ItemDataSO data, int count = 1) : base(data)
    {
        bootyType = data.bootyType;
        stackCount = Mathf.Max(1, count);
    }

    public void SetLootInfo(string monsterName)
    {
        sourceMonsterName = monsterName;
        Debug.Log($"[전리품] {monsterName}에게서 {data.itemName} 획득!");
    }

    public void UseAsIngredient(int amount)
    {
        IStackable stack = this;
        int removed = stack.RemoveStack(amount);
        Debug.Log($"[전리품] {data.itemName} {removed}개 소모 (남은 수량: {stackCount})");
    }

    public override string ToString()
    {
        string info = base.ToString();
        if (!string.IsNullOrEmpty(sourceMonsterName))
            info += $" (전리품 출처: {sourceMonsterName})";
        return info;
    }
}

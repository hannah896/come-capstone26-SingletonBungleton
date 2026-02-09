using NUnit.Framework.Interfaces;
using UnityEngine;

/// <summary>
/// 전리품 아이템 클래스
/// - 고기
/// - 가죽
/// - 깃털
/// - 뿔
/// </summary>

public class Item_Booty : Item
{
    [Header("=== 전리품 전용 속성 ===")]
    [Tooltip("전리품 세부 타입")]
    public BootyType lootType = BootyType.None;

    [Tooltip("어떤 몬스터에게서 나온 전리품인지")]
    public string sourceMonsterName;


    protected override void Init()
    {
        base.Init();
    }

    protected override void Init(ItemDataSO data)
    {
        base.Init(data);
    }

//전리품 정보 설정 (드롭할때 호출)
    /// <param name="monsterName">몬스터 이름</param>
    /// <param name="isRare">희귀 드롭 여부</param>
    public void SetLootInfo(string monsterName)
    {
        sourceMonsterName = monsterName;

        Debug.Log($"{monsterName}에게서 {itemData.itemName} 획득!");
    }

//전리품 사용
    public void UseAsIngredient(int amount)
    {
        int removed = RemoveStack(amount);
        Debug.Log($"{itemData.itemName} {removed}개 사용됨");

        if (stackCount <= 0)
        {
            Destroy(gameObject);
        }
    }

//UI
    public override string ToString()
    {
        string baseInfo = base.ToString();

        if (!string.IsNullOrEmpty(sourceMonsterName))
        {
            baseInfo += $" (출처: {sourceMonsterName})";
        }

        return baseInfo;
    }
}
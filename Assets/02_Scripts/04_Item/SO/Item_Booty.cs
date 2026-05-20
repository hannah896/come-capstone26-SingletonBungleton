using UnityEngine;

/// <summary>
/// 전리품 아이템 클래스
/// - 생고기
/// - 가죽
/// - 깃털
/// - 뿔
/// </summary>

public class Item_Booty : Item
{
    [Header("=== 전리품 전용 속성 ===")]
    [Tooltip("전리품 세부 타입")]
    public BootyType bootyType = BootyType.None;

    [Tooltip("드롭한 몬스터 이름")]
    public string sourceMonsterName;

    protected override void Init()
    {
        base.Init();
        if (itemData != null)
        {
            bootyType = itemData.bootyType;
        }
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        bootyType = itemData.bootyType;
    }

    // 몬스터가 드롭할 때 호출
    public void SetLootInfo(string monsterName)
    {
        sourceMonsterName = monsterName;
        Debug.Log($"[전리품] {monsterName}에게서 {itemData.itemName} 획득!");
    }

    // 크래프팅 재료로 사용
    public void UseAsIngredient(int amount)
    {
        int removed = RemoveStack(amount);
        Debug.Log($"[전리품] {itemData.itemName} {removed}개 소모(남은 수량: {stackCount})");

        if (stackCount <= 0)
            Destroy(gameObject);
    }


    public override string ToString()
    {
        string info = base.ToString();
        if (!string.IsNullOrEmpty(sourceMonsterName))
            info += $" (전리품 출처: {sourceMonsterName})";
        return info;
    }
}
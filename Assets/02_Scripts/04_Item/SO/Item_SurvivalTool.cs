using UnityEngine;

/// <summary>
/// 생존도구 런타임 데이터 클래스
/// - 도끼, 곡괭이, 망치, 삽, 낚싯대, 횃불
/// </summary>
public class Item_SurvivalTool : ItemData, IEquipable
{
    public SurvivalToolType survivalToolType;
    private float currentDurability;

    private float maxDurability => data.maxDurability;
    public float CurrentDurability => currentDurability;
    public float CostPerDurability => data.costPerDurability;

    private bool _isLit = false;

    public Item_SurvivalTool(ItemDataSO data, int count = 1) : base(data, count)
    {
        survivalToolType = data.survivalToolType;
        currentDurability = data.maxDurability;
    }

    #region IEquipable 구현
    public void Equip()
    {
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchActive(true);
    }

    public void Unequip()
    {
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchActive(false);
    }

    public void UseDurability()
    {
        currentDurability -= data.costPerDurability;
        if (currentDurability <= 0)
        {
            currentDurability = 0;
            Debug.Log($"[도구] {data.itemName}이(가) 부서졌습니다!");
        }
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability) return 1f;
        return currentDurability / maxDurability;
    }
    #endregion

    private void SetTorchActive(bool on)
    {
        _isLit = on;
        // 실제 Light 컴포넌트 활성화는 Item MonoBehaviour에서 처리
        Debug.Log($"[횃불] {(on ? "켜짐" : "꺼짐")}");
    }
}

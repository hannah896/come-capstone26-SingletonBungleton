public class Item_SurvivalTool : ItemData, IEquipable
{
    public SurvivalToolType survivalToolType = SurvivalToolType.None;
    private float currentDurability;

    private float maxDurability => data.maxDurability;
    public float CurrentDurability => currentDurability;
    public float CostPerDurability => data.costPerDurability;

    private bool _isLit = false;



    #region 초기화
    public Item_SurvivalTool(ItemDataSO data, int count = 1) : base(data, count)
    {
        currentDurability = data.maxDurability;
    }
    #endregion



    #region IEquipable 구현
    /// <summary>
    /// 효과음 재생, 불빛 세팅등의 기능 구현
    /// 스텟적인 적용은 Player에서 해줄것.
    /// </summary>
    public void Equip()
    {
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchLight(true);
    }

    public void Unequip()
    {
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchLight(false);
    }

    public void UseDurability()
    {
        currentDurability -= CostPerDurability;
        if (currentDurability <= 0)
            OnToolBroken();
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability) return 1f;
        return currentDurability / maxDurability;
    }
    #endregion

    #region 횃불
    private void SetTorchLight(bool on)
    {
        _isLit = on;

        if (torchLight != null)
            torchLight.enabled = on;
        else
            Debug.LogWarning("[횃불] torchLight가 연결되지 않았습니다");

        Debug.Log($"[횃불] {(on ? "켜짐" : "꺼짐")}");
    }
    #endregion
}
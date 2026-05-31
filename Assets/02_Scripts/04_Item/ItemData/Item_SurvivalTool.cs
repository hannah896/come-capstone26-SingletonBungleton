using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생존도구 런타임 데이터 클래스
/// - 도끼, 곡괭이, 망치, 삽, 낚싯대, 횃불
/// </summary>
public class Item_SurvivalTool : ItemData_Equipable
{
    public SurvivalToolType survivalToolType;
    public float CostPerDurability => data.costPerDurability;
    public IReadOnlyList<ResourceType> HarvestableResources => data.harvestableResources;

    public event Action<bool> OnTorchToggled;
    public bool IsLit => _isLit;
    private bool _isLit = false;

    public Item_SurvivalTool(ItemDataSO data) : base(data)
    {
        survivalToolType = data.survivalToolType;
    }

    public override void Equip()
    {
        if (!IsUsable) return;
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchActive(true);
    }

    public override void Unequip()
    {
        if (survivalToolType == SurvivalToolType.Torch)
            SetTorchActive(false);
    }

    private void SetTorchActive(bool on)
    {
        _isLit = on;
        OnTorchToggled?.Invoke(on);
    }
}

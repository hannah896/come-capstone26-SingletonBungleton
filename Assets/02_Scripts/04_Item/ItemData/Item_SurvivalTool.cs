using System;
using UnityEngine;

/// <summary>
/// 생존도구 런타임 데이터 클래스
/// - 도끼, 곡괭이, 망치, 삽, 낚싯대, 횃불
/// </summary>
public class Item_SurvivalTool : ItemData, IEquipable
{
    public SurvivalToolType survivalToolType;
    private float currentDurability;
    private bool isUsable = true;

    private float maxDurability => data.maxDurability;
    public event Action<IEquipable> OnBroken;
    public ItemDataSO ItemData => data;
    public float CurrentDurability => currentDurability;
    public float CostPerDurability => data.costPerDurability;
    public bool IsUsable
    {
        get => isUsable;
        set
        {
            if (isUsable == value) return;

            isUsable = value;
            if (isUsable) return;

            Unequip();
            OnBroken?.Invoke(this);
        }
    }

    public event Action<bool> OnTorchToggled;
    public bool IsLit => _isLit;
    private bool _isLit = false;

    public Item_SurvivalTool(ItemDataSO data) : base(data)
    {
        survivalToolType = data.survivalToolType;
        currentDurability = data.hasDurability ? data.maxDurability : 0f;
        isUsable = !data.hasDurability || currentDurability > 0f;
    }

    #region IEquipable 구현
    public void Equip()
    {
        if (!IsUsable) return;

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
        if (!data.hasDurability || !IsUsable) return;

        currentDurability = Mathf.Max(0f, currentDurability - data.costPerDurability);
        if (currentDurability <= 0f)
        {
            Debug.Log($"[도구] {data.itemName}이(가) 부서졌습니다!");
            IsUsable = false;
        }
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability) return 1f;
        return Mathf.Clamp01(currentDurability / Mathf.Max(0.001f, maxDurability));
    }
    #endregion

    private void SetTorchActive(bool on)
    {
        _isLit = on;
        OnTorchToggled?.Invoke(on);
    }
}

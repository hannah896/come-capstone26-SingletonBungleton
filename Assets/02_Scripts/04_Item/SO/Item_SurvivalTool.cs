using System;
using UnityEngine;

public class Item_SurvivalTool : Item, IEquipable
{
    [Header("=== 생존 도구 전용 ===")]
    public SurvivalToolType survivalToolType = SurvivalToolType.None;

    [Header("=== 횃불 ===")]
    [SerializeField] private Light torchLight;

    private float currentDurability;
    private bool isUsable = true;

    public event Action<IEquipable> OnBroken;

    public ItemDataSO ItemData => itemData;
    public float CurrentDurability => currentDurability;
    public float CostPerDurability => itemData != null ? itemData.costPerDurability : 0f;

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

    protected override void Init()
    {
        base.Init();
        InitializeToolData();
    }

    public override void Init(ItemDataSO data)
    {
        base.Init(data);
        InitializeToolData();
    }

    public void Equip()
    {
        if (!IsUsable) return;

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
        if (itemData == null || !itemData.hasDurability || !IsUsable) return;

        currentDurability = Mathf.Max(0f, currentDurability - CostPerDurability);
        if (currentDurability <= 0f)
            IsUsable = false;
    }

    public float GetDurabilityPercent()
    {
        if (itemData == null || !itemData.hasDurability) return 1f;

        float maxDurability = Mathf.Max(0.001f, itemData.maxDurability);
        return Mathf.Clamp01(currentDurability / maxDurability);
    }

    private void InitializeToolData()
    {
        if (itemData == null) return;

        survivalToolType = itemData.survivalToolType;
        currentDurability = itemData.hasDurability ? Mathf.Max(0f, itemData.maxDurability) : 0f;
        isUsable = !itemData.hasDurability || currentDurability > 0f;
    }

    private void SetTorchLight(bool on)
    {
        if (torchLight != null)
        {
            torchLight.enabled = on;
            return;
        }

        Debug.LogWarning("[횃불] torchLight가 연결되지 않았습니다");
    }
}

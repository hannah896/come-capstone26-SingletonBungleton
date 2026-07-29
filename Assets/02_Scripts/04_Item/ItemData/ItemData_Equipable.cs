using System;
using UnityEngine;

/// <summary>
/// 내구도를 가진 장착형 아이템(생존도구/전투장비)의 공통 베이스 클래스.
/// IEquipable의 공통 구현(내구도 관리, IsUsable, OnBroken)을 제공한다.
/// </summary>
public abstract class ItemData_Equipable : ItemData, IEquipable
{
    private float _currentDurability;
    private bool _isUsable = true;

    public event Action<IEquipable> OnBroken;
    public ItemDataSO ItemSO => data;
    public float CurrentDurability => _currentDurability;
    public bool IsUsable
    {
        get => _isUsable;
        set
        {
            if (_isUsable == value) return;
            _isUsable = value;
            if (_isUsable) return;
            Unequip();
            OnBroken?.Invoke(this);
        }
    }

    protected ItemData_Equipable(ItemDataSO data) : base(data)
    {
        _currentDurability = data.hasDurability ? data.maxDurability : 0f;
        _isUsable = !data.hasDurability || _currentDurability > 0f;
    }

    public void UseDurability()
    {
        if (!data.hasDurability || !IsUsable) return;
        _currentDurability = Mathf.Max(0f, _currentDurability - data.costPerDurability);
        if (_currentDurability <= 0f)
        {
            Debug.Log($"[장비] {data.itemName}이(가) 부서졌습니다!");
            IsUsable = false;
        }
    }

    public void DrainDurabilityOverTime(float deltaTime)
    {
        if (!data.hasDurability || !IsUsable) return;
        _currentDurability = Mathf.Max(0f, _currentDurability - data.costPerDurability * deltaTime);
        if (_currentDurability <= 0f)
        {
            Debug.Log($"[장비] {data.itemName}이(가) 다 닳았습니다!");
            IsUsable = false;
        }
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability) return 1f;
        return Mathf.Clamp01(_currentDurability / Mathf.Max(0.001f, data.maxDurability));
    }

    public abstract void Equip();
    public abstract void Unequip();
}

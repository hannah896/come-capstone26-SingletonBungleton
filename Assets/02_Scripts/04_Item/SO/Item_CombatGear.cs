using System;
using UnityEngine;

/// <summary>
/// 전투장비 런타임 데이터 클래스
/// - 헬멧, 갑옷, 칼, 활, 창, 방패
/// </summary>
public class Item_CombatGear : ItemData, IEquipable
{
    public CombatGearType combatGearType;

    private float _currentDurability;
    private bool isUsable = true;

    public event Action<IEquipable> OnBroken;
    public ItemDataSO ItemData => data;
    public float CurrentDurability => _currentDurability;
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

    public Item_CombatGear(ItemDataSO data, int count = 1) : base(data, count)
    {
        combatGearType = data.combatGearType;
        _currentDurability = data.hasDurability ? data.maxDurability : 0f;
        isUsable = !data.hasDurability || _currentDurability > 0f;
    }

    #region IEquipable 구현
    public void Equip()
    {
        if (!IsUsable) return;

        Debug.Log($"[장비] {data.itemName} 장착!");
    }

    public void Unequip()
    {
        Debug.Log($"[장비] {data.itemName} 해제!");
    }

    public void UseDurability()
    {
        if (!data.hasDurability || !IsUsable) return;

        _currentDurability = Mathf.Max(0f, _currentDurability - data.costPerDurability);
        Debug.Log($"[장비] 내구도 {_currentDurability}/{data.maxDurability}");

        if (_currentDurability <= 0f)
        {
            Debug.Log($"[장비] {data.itemName}이(가) 부서졌습니다!");
            IsUsable = false;
        }
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability) return 1f;
        return Mathf.Clamp01(_currentDurability / Mathf.Max(0.001f, data.maxDurability));
    }
    #endregion

    public void Repair(float amount)
    {
        if (!data.hasDurability) return;
        _currentDurability = Mathf.Min(_currentDurability + amount, data.maxDurability);
        isUsable = _currentDurability > 0f;
        Debug.Log($"[장비] {data.itemName} 수리! ({_currentDurability}/{data.maxDurability})");
    }

    public bool IsArmor() => combatGearType == CombatGearType.Helmet || combatGearType == CombatGearType.Chestplate;
    public bool IsShield() => combatGearType == CombatGearType.Shield;
    public bool IsWeapon() => combatGearType == CombatGearType.Sword || combatGearType == CombatGearType.Bow || combatGearType == CombatGearType.Spear;
    public bool CanAttack() => IsUsable && (!data.hasDurability || _currentDurability > 0f);

    public override string ToString()
    {
        string info = base.ToString();
        if (IsArmor()) info += $" [방어: {data.defense}]";
        if (IsWeapon()) info += $" [공격: {data.attackDamage}]";
        if (data.hasDurability) info += $" [내구도: {_currentDurability}/{data.maxDurability}]";
        return info;
    }
}

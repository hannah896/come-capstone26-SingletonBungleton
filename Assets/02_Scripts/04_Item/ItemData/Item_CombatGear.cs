using UnityEngine;

/// <summary>
/// 전투장비 런타임 데이터 클래스
/// - 헬멧, 갑옷, 칼, 활, 창, 방패
/// </summary>
public class Item_CombatGear : ItemData_Equipable
{
    public CombatGearType combatGearType;

    public Item_CombatGear(ItemDataSO data) : base(data)
    {
        combatGearType = data.combatGearType;
    }

    public override void Equip()
    {
        if (!IsUsable) return;
        Debug.Log($"[장비] {data.itemName} 장착!");
    }

    public override void Unequip()
    {
        Debug.Log($"[장비] {data.itemName} 해제!");
    }

    public bool IsArmor()   => combatGearType == CombatGearType.Helmet || combatGearType == CombatGearType.Chestplate;
    public bool IsShield()  => combatGearType == CombatGearType.Shield;
    public bool IsWeapon()  => combatGearType == CombatGearType.Sword || combatGearType == CombatGearType.Bow || combatGearType == CombatGearType.Spear;
    public bool CanAttack() => IsUsable && (!data.hasDurability || CurrentDurability > 0f);

    public override string ToString()
    {
        string info = base.ToString();
        if (IsArmor())          info += $" [방어: {data.defense}]";
        if (IsWeapon())         info += $" [공격: {data.attackDamage}]";
        if (data.hasDurability) info += $" [내구도: {CurrentDurability}/{data.maxDurability}]";
        return info;
    }
}

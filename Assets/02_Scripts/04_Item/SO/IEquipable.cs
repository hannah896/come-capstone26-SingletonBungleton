using System;

// 무기와 도구처럼 장착 가능한 아이템에 사용합니다.
public interface IEquipable
{
    event Action<IEquipable> OnBroken;

    ItemDataSO ItemData { get; }
    float CurrentDurability { get; }
    bool IsUsable { get; set; }

    float GetDurabilityPercent();
    void Equip();
    void Unequip();
    void UseDurability();
}

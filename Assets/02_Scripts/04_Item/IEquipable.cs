using System;

// 무기와 도구처럼 장착 가능한 아이템에 사용합니다.
public interface IEquipable
{
    event Action<IEquipable> OnBroken;

    ItemDataSO ItemSO { get; }
    float CurrentDurability { get; }
    bool IsUsable { get; set; }

    float GetDurabilityPercent();
    void Equip();
    void Unequip();
    void UseDurability();

    /// <summary>장착 중 시간 경과에 따라 내구도를 깎는다 (횃불처럼 지속 사용되는 도구용). costPerDurability를 초당 감소량으로 취급한다.</summary>
    void DrainDurabilityOverTime(float deltaTime);
}

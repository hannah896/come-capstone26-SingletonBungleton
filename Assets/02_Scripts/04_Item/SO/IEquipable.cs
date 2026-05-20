// 내구도를 가지는 아이템에 사용(무기, 도구)
public interface IEquipable
{
    public float CurrentDurability { get; }  // 현재 내구도
    public float GetDurabilityPercent();    // 내구도 퍼센트

    public void Equip();                      // 장착
    public void Unequip();                   // 해제
    public void UseDurability();            // 내구도 감소
}
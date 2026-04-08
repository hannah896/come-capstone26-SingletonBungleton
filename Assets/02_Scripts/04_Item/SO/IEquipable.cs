using UnityEngine;

// 내구도를 가지는 아이템에 사용(무기, 도구)
public interface IEquipable
{
    int CurrentDurability { get; set; }  // 현재 내구도
    void Equip();                      // 장착
    void Unequip();                   // 해제
    void UseDurability(int amount);  // 내구도 감소
    void Repair(int amount);         // 내구도 수리
    float GetDurabilityPercent();    // 내구도 퍼센트
}
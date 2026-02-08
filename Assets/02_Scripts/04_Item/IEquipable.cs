using UnityEngine;

// 내구도를 가지는 아이템에 사용
public interface IEquipable
{
    public int CurrentDurability { get; set; }      // 현재 내구도 (도구/무기)
    public void UseDurability (int durability);    // 내구도 감소
}
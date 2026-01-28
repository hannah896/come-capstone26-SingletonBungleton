using UnityEngine;

// 1. 아이템 대분류
public enum ItemType
{
    Material,   // 재료
    Food,       // 음식
    Tool,       // 도구
    Weapon,     // 무기
    Armor,      // 방어구
    Recipe      // 레시피
}

// 2. 도구 세부 분류
public enum ToolType
{
    None,
    Pickaxe,    // 곡괭이
    Axe         // 도끼
}

// 3. 무기 세부 분류
public enum WeaponType
{
    None,
    Sword,      // 칼
    Bow,        // 활
    Spear,      // 창
    Club        // 몽둥이
}

// 4. 장비 슬롯
public enum EquipSlot
{
    None,
    Head,       // 머리
    Body,       // 상의
    Hand,       // 손
    Shield      // 방패
}
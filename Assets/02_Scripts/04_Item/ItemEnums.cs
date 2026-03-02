using UnityEngine;

public enum ItemType
{
    SurvivalTool,   // 생존도구
    CombatGear,     // 전투도구
    Resource,       // 자원
    Booty,           // 전리품
    Food            // 음식
}

// 생존도구 세부 타입
public enum SurvivalToolType
{
    None,       
    Axe,        // 도끼
    Pickaxe,    // 곡괭이
    Torch       // 횃불
}

// 전투도구 세부 타입
public enum CombatGearType
{
    None,       
    Helmet,     // 헬멧
    Chestplate,      // 갑옷
    Sword,      // 칼
    Bow,        // 활
    Spear,      // 창
    Shield      // 방패
}

// 자원 세부 타입
public enum ResourceType
{
    None,
    Wood_Charcoal,  // 숯
    Wood_Plank,     // 장작
    Wood_Branch,    // 나뭇가지
    Wood_Berry,     // 열매

    Mineral_Gold,   // 금
    Mineral_Stone,  // 돌
    Mineral_Flint,  // 부싯돌

    Plant_Grass     // 풀
}

// 전리품 세부 타입
public enum BootyType
{
    None,       
    RawMeat,    // 생고기
    Leather,    // 가죽
    Feather,    // 깃털
    Horn        // 뿔
}


// 음식
public enum FoodType
{
    None,
    CookedMeat  //구운고기
}

//  장비 장착 위치
public enum EquipSlot
{
    None,
    Head, 
    Chest,
    Hand
}

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

    Axe_Stone,      // 돌도끼
    Axe_Iron,       // 철도끼
    Axe_Gold,       // 금도끼

    Pickaxe_Stone,  // 돌곡괭이
    Pickaxe_Iron,   // 철곡괭이
    Pickaxe_Gold,   // 금곡괭이

    Hammer_Stone,   // 돌망치
    Hammer_Iron,    // 철망치
    Hammer_Gold,    // 금망치

    Shovel_Stone,   // 돌삽
    Shovel_Iron,    // 철삽
    Shovel_Gold,    // 금삽

    FishingRod,     // 낚싯대

    Torch       // 횃불
}

// 전투도구 세부 타입
public enum CombatGearType
{
    None,       
    Helmet,     // 헬멧
    Chestplate, // 갑옷
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
    Berry,     // 열매
    Mineral_Gold,   // 금
    Mineral_Stone,  // 돌
    Mineral_Flint,  // 부싯돌
    Mineral_Iron,   // 철
    Mineral_Coal,   // 석탄

    Plant_Grass     // 풀
}

// 전리품 세부 타입
public enum BootyType
{
    None,       
    Meat1,      // 생고기
    Meat2,
    Meat3,
    Egg,        //알
    Leather,    // 가죽
    Feather,    // 깃털
    Horn        // 뿔
}


// 음식
public enum FoodType
{
    None,
    CookedMeat1,  //구운고기
    CookedMeat2,
    CookedMeat3,
    CookedEgg,  //구운 알
    RoastedBerry //구운베리
}

//  장비 장착 위치
public enum EquipSlot
{
    None,
    Head, 
    Chest,
    Hand
}

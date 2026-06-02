using System;

/// <summary>
/// 아이템이 어떤 용도로 쓰일 수 있는지 정의하는 플래그 태그.
/// 여러 태그를 동시에 가질 수 있다.
/// </summary>
[Flags]
public enum ItemTag
{
    None        = 0,
    Fuel        = 1 << 0,  // 연료 (횃불·모닥불 점화에 사용)
    Food        = 1 << 1,  // 바로 먹을 수 있는 음식
    Cookable    = 1 << 2,  // 조리 가능 (생고기, 알 등 → 익히면 Food)
    Ingredient  = 1 << 3,  // 크래프팅 재료
    Medicine    = 1 << 4,  // 치료제 (HP/Ego 회복)
    Structure   = 1 << 5,  // 배치 가능한 구조물 아이템
    Flammable   = 1 << 6,  // 불에 탈 수 있음
    Plantable   = 1 << 7,  // 심을 수 있음
}

/// <summary>
/// 자원 노드를 채집하기 위해 필요한 도구 종류.
/// ResourceNodeDataSO에서 사용.
/// </summary>
public enum HarvestToolType
{
    None       = 0,  // 제한 없음 (맨손 가능)
    Axe        = 1,  // 도끼   → 나무, 통나무
    Pickaxe    = 2,  // 곡괭이 → 돌, 광물 (철/금/석탄/부싯돌)
    Shovel     = 3,  // 삽     → 풀, 흙
    FishingRod = 4,  // 낚싯대 → 물고기
    Hammer     = 5,  // 망치   → 구조물 해체
    AnyTool    = 99, // 도구 필요 (맨손 불가, 종류 무관)
}

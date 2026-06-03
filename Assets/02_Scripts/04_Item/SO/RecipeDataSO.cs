using UnityEngine;

[CreateAssetMenu(fileName = "Recipe_", menuName = "Game/Recipe Data")]
public class RecipeDataSO : ScriptableObject
{
    [Header("=== 기본 정보 ===")]
    public string recipeName;

    [Header("=== 카테고리 ===")]
    public RecipeCategory category;

    [Header("=== 프로토타입 스테이션 ===")]
    [Tooltip("이 레시피를 처음 제작하기 위해 필요한 작업대. 한 번 제작하면 어디서든 가능.")]
    public CraftStation requiredStation = CraftStation.None;

    [Header("=== 필요 재료 ===")]
    public RecipeIngredient[] ingredients;

    [Header("=== 제작 결과 ===")]
    public ItemDataSO resultItem;
    public int resultAmount = 1;
}

[System.Serializable]
public struct RecipeIngredient
{
    public ItemDataSO itemData;
    public int amount;
}

public enum RecipeCategory
{
    Tools      = 0,  // 도구
    Light      = 1,  // 광원
    Weapons    = 2,  // 무기
    Armor      = 3,  // 방어구
    Survival   = 4,  // 생존
    Structures = 5,  // 구조물
    Medicine   = 6,  // 치료제
    All        = 7,  // 모두
}

// 작업대 티어 (높은 티어는 낮은 티어 레시피도 제작 가능)
public enum CraftStation
{
    None      = 0,  // 맨손 제작
    Workbench = 1,  // 작업대
    Forge     = 2,  // 용광로
}
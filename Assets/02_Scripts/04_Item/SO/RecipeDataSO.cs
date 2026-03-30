using UnityEngine;

/// <summary>
/// 크래프팅 레시피 데이터
/// - 필요 재료 목록
/// - 제작 결과 아이템
/// </summary>
/// 

[CreateAssetMenu(fileName = "Recipe_", menuName = "Game/Recipe Data")]
public class RecipeDataSO : ScriptableObject
{
    [Header("=== 기본 정보 ===")]
    public string recipeName;
    public Sprite icon;

    [Header("=== 카테고리 ===")]
    public RecipeCategory category;

    [Header("=== 필요 재료 ===")]
    public RecipeIngredient[] ingredients;

    [Header("=== 제작 결과 ===")]
    public ItemDataSO resultItem;
    public int resultAmount = 1;
}

// 재료 하나 (아이템 + 필요 수량)
[System.Serializable]
public struct RecipeIngredient
{
    public ItemDataSO itemData;
    public int amount;
}

public enum RecipeCategory
{
    Tools,      // 도구
    Light,      // 광원
    Survival,   // 생존
    Weapons,    // 무기
    Structures, // 건물
    Moon        // 달
}
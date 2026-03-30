using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("전체 레시피 목록")]
    public List<RecipeDataSO> allRecipes;

    public event System.Action OnCraftingChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 제작 가능 여부 확인 (재료만 체크)
    public bool CanCraft(RecipeDataSO recipe)
    {
        if (recipe == null) return false;

        foreach (var ingredient in recipe.ingredients)
        {
            if (!InventoryManager.Instance.HasItem(ingredient.itemData, ingredient.amount))
            {
                Debug.Log($"[크래프팅] {ingredient.itemData.itemName} {ingredient.amount}개 부족!");
                return false;
            }
        }
        return true;
    }

    // 제작 실행
    public bool Craft(RecipeDataSO recipe)
    {
        if (!CanCraft(recipe)) return false;

        foreach (var ingredient in recipe.ingredients)
            InventoryManager.Instance.RemoveItem(ingredient.itemData, ingredient.amount);

        bool success = InventoryManager.Instance.AddItem(recipe.resultItem, recipe.resultAmount);

        if (success)
            Debug.Log($"[크래프팅] {recipe.recipeName} 제작 완료!");
        else
            Debug.Log($"[크래프팅] 인벤토리 가득 참!");

        OnCraftingChanged?.Invoke();
        return success;
    }

    // 카테고리별 필터
    public List<RecipeDataSO> GetRecipesByCategory(RecipeCategory category)
    {
        List<RecipeDataSO> result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (recipe.category == category) result.Add(recipe);
        return result;
    }

    // 제작 가능한 레시피만 필터
    public List<RecipeDataSO> GetCraftableRecipes()
    {
        List<RecipeDataSO> result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (CanCraft(recipe)) result.Add(recipe);
        return result;
    }
}
//using System.Collections.Generic;
//using UnityEngine;

//public class CraftingManager : MonoBehaviour
//{
//    public static CraftingManager Instance { get; private set; }

//    [Header("전체 레시피 목록")]
//    public List<RecipeDataSO> allRecipes;

//    public event System.Action OnCraftingChanged;

//    private void Awake()
//    {
//        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
//        Instance = this;
//    }

//    // 제작 가능 여부 확인
//    public bool CanCraft(RecipeDataSO recipe)
//    {
//        if (recipe == null) return false;

//        if (recipe.resultItem == null) return false;

//        foreach (var ingredient in recipe.ingredients)
//        {
//            if (ingredient.itemData == null)
//            {
//                Debug.Log($"[크래프팅] {recipe.recipeName} 재료 데이터 없음");
//                return false;
//            }
//            if (!InventoryManager.Instance.HasItem(ingredient.itemData, ingredient.amount))
//            {
//                Debug.Log($"[크래프팅] {ingredient.itemData.itemName} {ingredient.amount}개 부족");
//                return false;
//            }
//        }
//        return true;
//    }

//    // 제작 실행
//    public bool Craft(RecipeDataSO recipe)
//    {
//        if (!CanCraft(recipe)) return false;

//        foreach (var ingredient in recipe.ingredients)
//            InventoryManager.Instance.RemoveItem(ingredient.itemData, ingredient.amount);

//        bool success = InventoryManager.Instance.AddItem(recipe.resultItem, recipe.resultAmount);

//        if (success)
//            Debug.Log($"[크래프팅] {recipe.recipeName} 제작 완료");
//        else
//        {
//            Debug.Log($"[크래프팅] 인벤토리 가득 참. 재료 반환");
//            foreach (var ingredient in recipe.ingredients)
//                InventoryManager.Instance.AddItem(ingredient.itemData, ingredient.amount);
//        }
//        OnCraftingChanged?.Invoke();
//        return success;
//    }

//    // 카테고리별 필터
//    public List<RecipeDataSO> GetRecipesByCategory(RecipeCategory category)
//    {
//        List<RecipeDataSO> result = new List<RecipeDataSO>();
//        foreach (var recipe in allRecipes)
//            if (recipe.category == category) result.Add(recipe);
//        return result;
//    }

//    // 제작 가능한 레시피만 필터
//    public List<RecipeDataSO> GetCraftableRecipes()
//    {
//        List<RecipeDataSO> result = new List<RecipeDataSO>();
//        foreach (var recipe in allRecipes)
//            if (CanCraft(recipe)) result.Add(recipe);
//        return result;
//    }

//    // 특정 아이템 제작 레시피 찾기
//    public RecipeDataSO GetRecipeByResult(ItemDataSO resultItem)
//    {
//        foreach (var recipe in allRecipes)
//            if (recipe.resultItem == resultItem) return recipe;
//        return null;
//    }

//    // 전체 레시피 목록 출력
//    public void PrintAllRecipes()
//    {
//        Debug.Log($"[크래프팅] 전체 레시피 {allRecipes.Count}개");
//        foreach (var recipe in allRecipes)
//        {
//            string ingredients = "";
//            foreach (var ing in recipe.ingredients)
//                ingredients += $"{ing.itemData?.itemName} x{ing.amount} ";
//            Debug.Log($"  [{recipe.category}] {recipe.recipeName} → 재료: {ingredients}");
//        }
//    }
//}
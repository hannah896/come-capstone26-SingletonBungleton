using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크래프팅 로직 매니저.
/// Player.Start() 또는 UI에서 Bind(playerInventory)를 호출해 인벤토리와 연결한다.
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("전체 레시피 목록")]
    public List<RecipeDataSO> allRecipes = new();

    private PlayerInventory inventory;

    public event System.Action OnCraftingChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>인벤토리 연결. Player가 초기화된 후 호출한다.</summary>
    public void Bind(PlayerInventory playerInventory)
    {
        inventory = playerInventory;
    }

    /// <summary>재료가 충분한지 확인한다.</summary>
    public bool CanCraft(RecipeDataSO recipe)
    {
        if (recipe == null || recipe.resultItem == null || inventory == null) return false;

        foreach (var ingredient in recipe.ingredients)
        {
            if (ingredient.itemData == null)
            {
                Debug.LogWarning($"[크래프팅] {recipe.recipeName}: 재료 데이터 없음");
                return false;
            }
            if (!inventory.HasItem(ingredient.itemData, ingredient.amount))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 크래프팅 실행. 성공 시 재료 소모 후 결과 아이템을 인벤토리에 추가.
    /// 인벤토리가 가득 찬 경우 재료를 반환하고 false를 돌려준다.
    /// </summary>
    public bool Craft(RecipeDataSO recipe)
    {
        if (!CanCraft(recipe)) return false;

        foreach (var ingredient in recipe.ingredients)
            inventory.RemoveItem(ingredient.itemData, ingredient.amount);

        bool success = inventory.AddItem(recipe.resultItem, recipe.resultAmount);

        if (!success)
        {
            Debug.LogWarning($"[크래프팅] 인벤토리 가득 참 — {recipe.recipeName} 재료 반환");
            foreach (var ingredient in recipe.ingredients)
                inventory.AddItem(ingredient.itemData, ingredient.amount);
        }
        else
        {
            Debug.Log($"[크래프팅] {recipe.recipeName} 제작 완료");
        }

        OnCraftingChanged?.Invoke();
        return success;
    }

    /// <summary>카테고리별 레시피 목록 반환.</summary>
    public List<RecipeDataSO> GetRecipesByCategory(RecipeCategory category)
    {
        var result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (recipe.category == category) result.Add(recipe);
        return result;
    }

    /// <summary>현재 인벤토리 기준 제작 가능한 레시피만 반환.</summary>
    public List<RecipeDataSO> GetCraftableRecipes()
    {
        var result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (CanCraft(recipe)) result.Add(recipe);
        return result;
    }

    /// <summary>결과 아이템으로 레시피를 검색한다.</summary>
    public RecipeDataSO GetRecipeByResult(ItemDataSO resultItem)
    {
        foreach (var recipe in allRecipes)
            if (recipe.resultItem == resultItem) return recipe;
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터 전용: 프로젝트 내 모든 RecipeDataSO를 allRecipes에 자동 로드한다.
    /// CraftingManager 컴포넌트 우클릭 → "모든 레시피 자동 로드" 로 실행.
    /// </summary>
    [ContextMenu("모든 레시피 자동 로드")]
    public void LoadAllRecipesEditor()
    {
        allRecipes.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RecipeDataSO");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<RecipeDataSO>(path);
            if (recipe != null) allRecipes.Add(recipe);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[CraftingManager] 레시피 {allRecipes.Count}개 자동 로드됨");
    }

    private void OnValidate()
    {
        // 씬/컴포넌트가 에디터에서 열릴 때 자동 갱신
        if (allRecipes.Count == 0)
            LoadAllRecipesEditor();
    }
#endif
}

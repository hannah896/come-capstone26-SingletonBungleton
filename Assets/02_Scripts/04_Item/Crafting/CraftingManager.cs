using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("전체 레시피 목록")]
    public List<RecipeDataSO> allRecipes = new();

    private PlayerInventory inventory;
    private readonly HashSet<string> learnedRecipes = new();
    private CraftStation nearbyStation = CraftStation.None;

    private const string LearnedPrefKey = "CraftingManager_Learned";

    public event Action OnCraftingChanged;
    public event Action<RecipeDataSO, ItemDataSO, int> OnCrafted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadLearnedRecipes();
    }

    public void Bind(PlayerInventory playerInventory)
    {
        inventory = playerInventory;
    }

    // ── 스테이션 ────────────────────────────────────────────────

    /// <summary>플레이어가 작업대에 입장/퇴장할 때 호출한다.</summary>
    public void SetNearbyStation(CraftStation station)
    {
        nearbyStation = station;
        OnCraftingChanged?.Invoke();
    }

    public CraftStation GetNearbyStation() => nearbyStation;

    // ── 레시피 잠금/해금 ─────────────────────────────────────────

    /// <summary>이 레시피를 제작 가능한 상태인지 확인 (재료와 무관하게 스테이션/해금 조건만).</summary>
    public bool IsUnlocked(RecipeDataSO recipe)
    {
        if (recipe.requiredStation == CraftStation.None) return true;
        if (learnedRecipes.Contains(recipe.name)) return true;
        return nearbyStation >= recipe.requiredStation;
    }

    /// <summary>잠겨 있는 레시피인지 (스테이션 없고 미해금).</summary>
    public bool IsLocked(RecipeDataSO recipe) => !IsUnlocked(recipe);

    /// <summary>이 레시피를 이전에 프로토타입한 적 있는지.</summary>
    public bool IsLearned(RecipeDataSO recipe) =>
        recipe.requiredStation == CraftStation.None || learnedRecipes.Contains(recipe.name);

    // ── 크래프팅 로직 ────────────────────────────────────────────

    public bool CanCraft(RecipeDataSO recipe)
    {
        if (recipe == null || recipe.resultItem == null || inventory == null) return false;
        if (IsLocked(recipe)) return false;

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
            // 스테이션이 필요했던 레시피는 최초 제작 시 영구 해금
            if (recipe.requiredStation != CraftStation.None && !learnedRecipes.Contains(recipe.name))
            {
                learnedRecipes.Add(recipe.name);
                SaveLearnedRecipes();
                Debug.Log($"[크래프팅] {recipe.recipeName} 레시피 해금됨");
            }

            Debug.Log($"[크래프팅] {recipe.recipeName} 제작 완료");
            OnCrafted?.Invoke(recipe, recipe.resultItem, recipe.resultAmount);
        }

        OnCraftingChanged?.Invoke();
        return success;
    }

    // ── 레시피 조회 ──────────────────────────────────────────────

    /// <param name="onlyCraftable">true면 현재 재료가 충분한 것만 반환.</param>
    public List<RecipeDataSO> GetRecipesByCategory(RecipeCategory category, bool onlyCraftable = false)
    {
        var result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
        {
            if (category != RecipeCategory.All && recipe.category != category) continue;
            if (onlyCraftable && !CanCraft(recipe)) continue;
            result.Add(recipe);
        }
        return result;
    }

    public List<RecipeDataSO> GetCraftableRecipes()
    {
        var result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (CanCraft(recipe)) result.Add(recipe);
        return result;
    }

    public RecipeDataSO GetRecipeByResult(ItemDataSO resultItem)
    {
        foreach (var recipe in allRecipes)
            if (recipe.resultItem == resultItem) return recipe;
        return null;
    }

    // ── 저장/불러오기 ─────────────────────────────────────────────

    private void SaveLearnedRecipes()
    {
        PlayerPrefs.SetString(LearnedPrefKey, string.Join(",", learnedRecipes));
        PlayerPrefs.Save();
    }

    private void LoadLearnedRecipes()
    {
        string saved = PlayerPrefs.GetString(LearnedPrefKey, string.Empty);
        if (string.IsNullOrEmpty(saved)) return;
        foreach (var entry in saved.Split(','))
            if (!string.IsNullOrEmpty(entry)) learnedRecipes.Add(entry);
    }

    /// <summary>개발용: 모든 학습 데이터 초기화.</summary>
    [ContextMenu("해금 데이터 초기화")]
    public void ClearLearnedRecipes()
    {
        learnedRecipes.Clear();
        PlayerPrefs.DeleteKey(LearnedPrefKey);
        OnCraftingChanged?.Invoke();
    }

#if UNITY_EDITOR
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
        if (allRecipes.Count == 0)
            LoadAllRecipesEditor();
    }
#endif
}

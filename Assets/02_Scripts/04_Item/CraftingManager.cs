using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("전체 레시피 목록")]
    public List<RecipeDataSO> allRecipes;

    [SerializeField] private PlayerInventory playerInventory;

    public event System.Action OnCraftingChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Bind(PlayerInventory inventory)
    {
        playerInventory = inventory;
    }

    public bool CanCraft(RecipeDataSO recipe)
    {
        if (recipe == null) return false;

        PlayerInventory inventory = ResolveInventory();
        if (inventory == null) return false;

        foreach (var ingredient in recipe.ingredients)
        {
            if (!inventory.HasItem(ingredient.itemData, ingredient.amount))
            {
                Debug.Log($"[크래프팅] {ingredient.itemData.itemName} {ingredient.amount}개 부족");
                return false;
            }
        }

        return true;
    }

    public bool Craft(RecipeDataSO recipe)
    {
        if (!CanCraft(recipe)) return false;

        PlayerInventory inventory = ResolveInventory();
        if (inventory == null) return false;

        foreach (var ingredient in recipe.ingredients)
            inventory.RemoveItem(ingredient.itemData, ingredient.amount);

        bool success = inventory.AddItem(recipe.resultItem, recipe.resultAmount);

        if (success)
            Debug.Log($"[크래프팅] {recipe.recipeName} 제작 완료!");
        else
            Debug.Log("[크래프팅] 인벤토리 가득 참");

        OnCraftingChanged?.Invoke();
        return success;
    }

    public List<RecipeDataSO> GetRecipesByCategory(RecipeCategory category)
    {
        List<RecipeDataSO> result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (recipe.category == category) result.Add(recipe);

        return result;
    }

    public List<RecipeDataSO> GetCraftableRecipes()
    {
        List<RecipeDataSO> result = new List<RecipeDataSO>();
        foreach (var recipe in allRecipes)
            if (CanCraft(recipe)) result.Add(recipe);

        return result;
    }

    private PlayerInventory ResolveInventory()
    {
        if (playerInventory != null) return playerInventory;

        playerInventory = FindFirstObjectByType<PlayerInventory>();
        return playerInventory;
    }
}

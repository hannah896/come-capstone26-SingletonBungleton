using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingUI : UI_Panel
{
    [Header("카테고리 탭 (순서: Tools, Light, Survival, Weapons, Structures, Moon)")]
    [SerializeField] private Button[] categoryTabButtons;

    [Header("레시피 목록")]
    [SerializeField] private Transform recipeListContainer;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("레시피 상세")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image resultIcon;
    [SerializeField] private TextMeshProUGUI resultNameText;
    [SerializeField] private TextMeshProUGUI resultAmountText;
    [SerializeField] private Transform ingredientContainer;
    [SerializeField] private GameObject ingredientSlotPrefab;
    [SerializeField] private Button craftButton;
    [SerializeField] private TextMeshProUGUI craftButtonText;

    private static readonly RecipeCategory[] Categories =
    {
        RecipeCategory.Tools,
        RecipeCategory.Light,
        RecipeCategory.Survival,
        RecipeCategory.Weapons,
        RecipeCategory.Structures,
        RecipeCategory.Moon,
    };

    private static readonly Color TabSelectedColor   = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Color TabDefaultColor    = Color.white;

    private readonly List<CraftingRecipeSlotUI> recipeSlotUIs = new();
    private RecipeCategory selectedCategory = RecipeCategory.Tools;
    private RecipeDataSO selectedRecipe;
    private PlayerInventory playerInventory;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        SetupCategoryTabs();
        return true;
    }

    private void OnEnable()
    {
        if (CraftingManager.Instance != null)
            CraftingManager.Instance.OnCraftingChanged += OnCraftingChanged;
    }

    private void OnDisable()
    {
        if (CraftingManager.Instance != null)
            CraftingManager.Instance.OnCraftingChanged -= OnCraftingChanged;
    }

    public void Bind(PlayerInventory inventory)
    {
        playerInventory = inventory;
        RefreshDetail();
    }

    public void OpenDefault() => SelectCategory(RecipeCategory.Tools);

    public void Open(RecipeCategory category) => SelectCategory(category);

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
        if (gameObject.activeSelf)
            OpenDefault();
    }

    public void SelectCategory(RecipeCategory category)
    {
        selectedCategory = category;
        selectedRecipe = null;

        RefreshCategoryTabs();
        RefreshRecipeList();

        // 카테고리 전환 시 첫 레시피 자동 선택
        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(category);
            if (recipes.Count > 0)
                selectedRecipe = recipes[0];
        }

        RefreshDetail();
    }

    public void SelectRecipe(RecipeDataSO recipe)
    {
        selectedRecipe = recipe;
        RefreshRecipeList();
        RefreshDetail();
    }

    public void Craft()
    {
        if (selectedRecipe == null || CraftingManager.Instance == null) return;
        CraftingManager.Instance.Craft(selectedRecipe);
    }

    private void OnCraftingChanged()
    {
        RefreshRecipeList();
        RefreshDetail();
    }

    private void SetupCategoryTabs()
    {
        if (categoryTabButtons == null) return;
        for (int i = 0; i < categoryTabButtons.Length && i < Categories.Length; i++)
        {
            int index = i;
            categoryTabButtons[i]?.onClick.AddListener(() => SelectCategory(Categories[index]));
        }
    }

    private void RefreshCategoryTabs()
    {
        if (categoryTabButtons == null) return;
        for (int i = 0; i < categoryTabButtons.Length && i < Categories.Length; i++)
        {
            if (categoryTabButtons[i] == null) continue;
            bool selected = Categories[i] == selectedCategory;
            var colors = categoryTabButtons[i].colors;
            colors.normalColor = selected ? TabSelectedColor : TabDefaultColor;
            categoryTabButtons[i].colors = colors;
        }
    }

    private void RefreshRecipeList()
    {
        if (recipeListContainer == null || recipeSlotPrefab == null || CraftingManager.Instance == null) return;

        List<RecipeDataSO> recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory);

        while (recipeSlotUIs.Count < recipes.Count)
        {
            var go = Instantiate(recipeSlotPrefab, recipeListContainer);
            var slot = go.GetComponent<CraftingRecipeSlotUI>() ?? go.AddComponent<CraftingRecipeSlotUI>();
            slot.OnSelected += SelectRecipe;
            recipeSlotUIs.Add(slot);
        }

        for (int i = 0; i < recipeSlotUIs.Count; i++)
        {
            if (i < recipes.Count)
            {
                bool canCraft  = CraftingManager.Instance.CanCraft(recipes[i]);
                bool isSelected = recipes[i] == selectedRecipe;
                recipeSlotUIs[i].Bind(recipes[i], canCraft, isSelected);
                recipeSlotUIs[i].gameObject.SetActive(true);
            }
            else
            {
                recipeSlotUIs[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshDetail()
    {
        if (detailPanel != null)
            detailPanel.SetActive(selectedRecipe != null);

        if (selectedRecipe == null) return;

        if (resultIcon != null)
        {
            resultIcon.sprite = selectedRecipe.resultItem?.icon;
            resultIcon.enabled = resultIcon.sprite != null;
        }

        if (resultNameText != null)
            resultNameText.text = selectedRecipe.resultItem?.itemName ?? string.Empty;

        if (resultAmountText != null)
            resultAmountText.text = selectedRecipe.resultAmount > 1 ? $"x{selectedRecipe.resultAmount}" : string.Empty;

        RefreshIngredients();
        RefreshCraftButton();
    }

    private void RefreshIngredients()
    {
        if (ingredientContainer == null || ingredientSlotPrefab == null || selectedRecipe == null) return;

        foreach (Transform child in ingredientContainer)
            Destroy(child.gameObject);

        foreach (var ingredient in selectedRecipe.ingredients)
        {
            var go = Instantiate(ingredientSlotPrefab, ingredientContainer);
            var slot = go.GetComponent<CraftingIngredientSlotUI>() ?? go.AddComponent<CraftingIngredientSlotUI>();
            int haveCount = playerInventory != null ? playerInventory.GetItemCount(ingredient.itemData) : 0;
            slot.Bind(ingredient, haveCount);
        }
    }

    private void RefreshCraftButton()
    {
        if (craftButton == null || CraftingManager.Instance == null) return;
        bool canCraft = selectedRecipe != null && CraftingManager.Instance.CanCraft(selectedRecipe);
        craftButton.interactable = canCraft;
        if (craftButtonText != null)
            craftButtonText.text = canCraft ? "제작" : "재료 부족";
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 작업대(Workbench)를 E키로 상호작용했을 때 뜨는 무기/방어구 제작 전용 팝업.
/// 화덕 팝업(UI_Popup_CookingPot)과 동일한 구조이되, 탭 2개(무기/방어구)로 카테고리를 전환한다.
/// </summary>
public class UI_Popup_Workbench : UI_Popup
{
    [Header("카테고리 탭 (순서: Weapons, Armor)")]
    [SerializeField] private Button[] categoryTabButtons;

    [Header("레시피 목록")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform recipeListContainer;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("레시피 상세")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Transform ingredientContainer;
    [SerializeField] private GameObject ingredientSlotPrefab;
    [SerializeField] private Button craftButton;
    [SerializeField] private TextMeshProUGUI craftButtonText;

    [Header("필터 토글 (선택)")]
    [SerializeField] private Button filterToggleButton;
    [SerializeField] private TextMeshProUGUI filterButtonText;

    private static readonly RecipeCategory[] Categories = { RecipeCategory.Weapons, RecipeCategory.Armor };
    private static readonly Color TabSelectedColor = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Color TabDefaultColor = Color.white;

    private readonly List<CraftingRecipeSlotUI> recipeSlotUIs = new();
    private RecipeCategory selectedCategory = RecipeCategory.Weapons;
    private int selectedCategoryIndex = 0;
    private RecipeDataSO selectedRecipe;
    private int selectedRecipeIndex = 0;
    private bool showOnlyCraftable = false;
    private PlayerInventory playerInventory;

    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(Close);
        craftButton?.onClick.AddListener(Craft);
        filterToggleButton?.onClick.AddListener(ToggleFilter);
        SetupCategoryTabs();
        if (titleText != null)
            titleText.text = "작업대";
        RefreshFilterButton();
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
        Unbind();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tabKey.wasPressedThisFrame)
        {
            int dir = kb.shiftKey.isPressed ? -1 : 1;
            SelectCategoryByIndex((selectedCategoryIndex + dir + Categories.Length) % Categories.Length);
        }

        if (kb.downArrowKey.wasPressedThisFrame) MoveRecipeSelection(1);
        if (kb.upArrowKey.wasPressedThisFrame) MoveRecipeSelection(-1);
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Craft();
        if (kb.fKey.wasPressedThisFrame) ToggleFilter();
    }

    public void Bind(PlayerInventory inventory)
    {
        Unbind();
        playerInventory = inventory;

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += OnCraftingChanged;

        SelectCategory(RecipeCategory.Weapons);
    }

    private void Unbind()
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= OnCraftingChanged;
        playerInventory = null;
    }

    public override void Close()
    {
        Unbind();
        base.Close();
    }

    public void SelectCategory(RecipeCategory category)
    {
        selectedCategory = category;
        selectedRecipeIndex = 0;

        for (int i = 0; i < Categories.Length; i++)
            if (Categories[i] == category) { selectedCategoryIndex = i; break; }

        selectedRecipe = null;
        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(category, showOnlyCraftable);
            if (recipes.Count > 0) selectedRecipe = recipes[0];
        }

        RefreshCategoryTabs();
        RefreshRecipeList();
        RefreshDetail();
    }

    public void SelectRecipe(RecipeDataSO recipe)
    {
        selectedRecipe = recipe;

        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, showOnlyCraftable);
            int idx = recipes.IndexOf(recipe);
            selectedRecipeIndex = idx >= 0 ? idx : 0;
        }

        RefreshRecipeList();
        RefreshDetail();
    }

    public void Craft()
    {
        if (selectedRecipe == null || CraftingManager.Instance == null) return;
        CraftingManager.Instance.Craft(selectedRecipe);
    }

    public void ToggleFilter()
    {
        showOnlyCraftable = !showOnlyCraftable;
        selectedRecipeIndex = 0;
        selectedRecipe = null;

        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, showOnlyCraftable);
            if (recipes.Count > 0) selectedRecipe = recipes[0];
        }

        RefreshFilterButton();
        RefreshRecipeList();
        RefreshDetail();
    }

    private void SelectCategoryByIndex(int index)
    {
        selectedCategoryIndex = index;
        SelectCategory(Categories[index]);
    }

    private void MoveRecipeSelection(int dir)
    {
        if (CraftingManager.Instance == null) return;
        var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, showOnlyCraftable);
        if (recipes.Count == 0) return;

        selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex + dir, 0, recipes.Count - 1);
        selectedRecipe = recipes[selectedRecipeIndex];
        RefreshRecipeList();
        RefreshDetail();
    }

    private void OnCraftingChanged()
    {
        if (showOnlyCraftable && selectedRecipe != null && CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, true);
            if (!recipes.Contains(selectedRecipe))
            {
                selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, Mathf.Max(0, recipes.Count - 1));
                selectedRecipe = recipes.Count > 0 ? recipes[selectedRecipeIndex] : null;
            }
        }

        RefreshRecipeList();
        RefreshDetail();
    }

    private void SetupCategoryTabs()
    {
        if (categoryTabButtons == null) return;
        for (int i = 0; i < categoryTabButtons.Length && i < Categories.Length; i++)
        {
            if (categoryTabButtons[i] == null) continue;
            int index = i;
            categoryTabButtons[i].onClick.AddListener(() => SelectCategory(Categories[index]));
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

    private void RefreshFilterButton()
    {
        if (filterButtonText != null)
            filterButtonText.text = showOnlyCraftable ? "제작 가능만 [F]" : "전체 보기 [F]";
    }

    private void RefreshRecipeList()
    {
        if (recipeListContainer == null || recipeSlotPrefab == null || CraftingManager.Instance == null) return;

        var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, showOnlyCraftable);

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
                bool canCraft = CraftingManager.Instance.CanCraft(recipes[i]);
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

        bool canCraft = CraftingManager.Instance.CanCraft(selectedRecipe);
        craftButton.interactable = canCraft;

        if (craftButtonText != null)
            craftButtonText.text = canCraft ? "제작 [Space]" : "재료 부족";
    }
}

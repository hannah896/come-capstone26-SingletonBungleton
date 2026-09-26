using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 화덕(CookingPot)을 E키로 상호작용했을 때 뜨는 요리 전용 팝업.
/// 크래프팅 UI의 "요리" 카테고리를 그대로 떼어내 여기로 옮긴 것 — 로직은
/// CraftingUI/CraftingManager를 그대로 재사용하고, 카테고리 탭 없이 Cooking 하나만 보여준다.
/// </summary>
public class UI_Popup_CookingPot : UI_Popup
{
    [Header("레시피 목록")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button closeButton;
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

    [Header("필터 토글 (선택)")]
    [SerializeField] private Button filterToggleButton;
    [SerializeField] private TextMeshProUGUI filterButtonText;

    private const RecipeCategory Category = RecipeCategory.Cooking;

    private readonly List<CraftingRecipeSlotUI> recipeSlotUIs = new();
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
        if (titleText != null)
            titleText.text = "화덕";
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

        selectedRecipeIndex = 0;
        selectedRecipe = null;
        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, showOnlyCraftable);
            if (recipes.Count > 0) selectedRecipe = recipes[0];
        }

        RefreshRecipeList();
        RefreshDetail();
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

    public void SelectRecipe(RecipeDataSO recipe)
    {
        selectedRecipe = recipe;

        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, showOnlyCraftable);
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
            var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, showOnlyCraftable);
            if (recipes.Count > 0) selectedRecipe = recipes[0];
        }

        RefreshFilterButton();
        RefreshRecipeList();
        RefreshDetail();
    }

    private void MoveRecipeSelection(int dir)
    {
        if (CraftingManager.Instance == null) return;
        var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, showOnlyCraftable);
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
            var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, true);
            if (!recipes.Contains(selectedRecipe))
            {
                selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, Mathf.Max(0, recipes.Count - 1));
                selectedRecipe = recipes.Count > 0 ? recipes[selectedRecipeIndex] : null;
            }
        }

        RefreshRecipeList();
        RefreshDetail();
    }

    private void RefreshFilterButton()
    {
        if (filterButtonText != null)
            filterButtonText.text = showOnlyCraftable ? "제작 가능만 [F]" : "전체 보기 [F]";
    }

    private void RefreshRecipeList()
    {
        if (recipeListContainer == null || recipeSlotPrefab == null || CraftingManager.Instance == null) return;

        var recipes = CraftingManager.Instance.GetRecipesByCategory(Category, showOnlyCraftable);

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

        bool canCraft = CraftingManager.Instance.CanCraft(selectedRecipe);
        craftButton.interactable = canCraft;

        if (craftButtonText != null)
            craftButtonText.text = canCraft ? "요리 [Space]" : "재료 부족";
    }
}

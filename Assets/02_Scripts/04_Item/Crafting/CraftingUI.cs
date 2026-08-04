using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CraftingUI : UI_Panel
{
    [Header("카테고리 탭 (순서: Tools, Light, Weapons, Armor, Survival, Structures, Medicine, Cooking, All)")]
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

    [Header("필터 토글 (선택)")]
    [SerializeField] private Button filterToggleButton;
    [SerializeField] private TextMeshProUGUI filterButtonText;

    [Header("스테이션 안내 (선택)")]
    [SerializeField] private TextMeshProUGUI stationHintText;

    // 배열 인덱스 = Inspector categoryTabButtons 배열 순서와 반드시 일치
    private static readonly RecipeCategory[] Categories =
    {
        RecipeCategory.Tools,      // [0] 도구
        RecipeCategory.Light,      // [1] 광원
        RecipeCategory.Weapons,    // [2] 무기
        RecipeCategory.Armor,      // [3] 방어구
        RecipeCategory.Survival,   // [4] 생존
        RecipeCategory.Structures, // [5] 구조물
        RecipeCategory.Medicine,   // [6] 치료제
        RecipeCategory.Cooking,    // [7] 요리
        RecipeCategory.All,        // [8] 전체
    };

    private static readonly Color TabSelectedColor = new(1f, 0.8f, 0.2f, 1f);
    private static readonly Color TabDefaultColor  = Color.white;

    private readonly List<CraftingRecipeSlotUI> recipeSlotUIs = new();
    private RecipeCategory selectedCategory = RecipeCategory.Tools;
    private RecipeDataSO selectedRecipe;
    private PlayerInventory playerInventory;

    private int selectedCategoryIndex = 0;
    private int selectedRecipeIndex   = 0;
    private bool showOnlyCraftable    = false;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        SetupCategoryTabs();
        SetupFilterButton();
        craftButton?.onClick.AddListener(Craft);
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
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= OnCraftingChanged;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Tab / Shift+Tab: 카테고리 전환
        if (kb.tabKey.wasPressedThisFrame)
        {
            int dir = kb.shiftKey.isPressed ? -1 : 1;
            SelectCategoryByIndex((selectedCategoryIndex + dir + Categories.Length) % Categories.Length);
        }

        // 방향키: 레시피 선택
        if (kb.downArrowKey.wasPressedThisFrame) MoveRecipeSelection(1);
        if (kb.upArrowKey.wasPressedThisFrame)   MoveRecipeSelection(-1);

        // Space / Enter: 제작
        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            Craft();

        // F: 필터 토글
        if (kb.fKey.wasPressedThisFrame)
            ToggleFilter();
    }

    // ── 공개 API ────────────────────────────────────────────────

    public void Bind(PlayerInventory inventory)
    {
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= OnCraftingChanged;

        playerInventory = inventory;

        if (playerInventory != null)
            playerInventory.OnInventoryChanged += OnCraftingChanged;

        RefreshDetail();
    }

    public void OpenDefault() => SelectCategory(RecipeCategory.Tools);

    public void Open(RecipeCategory category) => SelectCategory(category);

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
        if (gameObject.activeSelf) OpenDefault();
    }

    public void SelectCategory(RecipeCategory category)
    {
        selectedCategory    = category;
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
        showOnlyCraftable   = !showOnlyCraftable;
        selectedRecipeIndex = 0;
        selectedRecipe      = null;

        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetRecipesByCategory(selectedCategory, showOnlyCraftable);
            if (recipes.Count > 0) selectedRecipe = recipes[0];
        }

        RefreshFilterButton();
        RefreshRecipeList();
        RefreshDetail();
    }

    // ── 내부 ────────────────────────────────────────────────────

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
        // 현재 selectedRecipe가 목록에서 사라진 경우 보정
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
            int index = i;
            categoryTabButtons[i]?.onClick.AddListener(() => SelectCategory(Categories[index]));
        }
    }

    private void SetupFilterButton()
    {
        filterToggleButton?.onClick.AddListener(ToggleFilter);
        RefreshFilterButton();
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
            var go   = Instantiate(recipeSlotPrefab, recipeListContainer);
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
                bool isLocked  = CraftingManager.Instance.IsLocked(recipes[i]);
                bool isLearned = CraftingManager.Instance.IsLearned(recipes[i]);
                recipeSlotUIs[i].Bind(recipes[i], canCraft, isSelected, isLocked, isLearned);
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
            resultIcon.sprite  = selectedRecipe.resultItem?.icon;
            resultIcon.enabled = resultIcon.sprite != null;
        }

        if (resultNameText != null)
            resultNameText.text = selectedRecipe.resultItem?.itemName ?? string.Empty;

        if (resultAmountText != null)
            resultAmountText.text = selectedRecipe.resultAmount > 1 ? $"x{selectedRecipe.resultAmount}" : string.Empty;

        RefreshIngredients();
        RefreshCraftButton();
        RefreshStationHint();
    }

    private void RefreshIngredients()
    {
        if (ingredientContainer == null || ingredientSlotPrefab == null || selectedRecipe == null) return;

        foreach (Transform child in ingredientContainer)
            Destroy(child.gameObject);

        foreach (var ingredient in selectedRecipe.ingredients)
        {
            var go   = Instantiate(ingredientSlotPrefab, ingredientContainer);
            var slot = go.GetComponent<CraftingIngredientSlotUI>() ?? go.AddComponent<CraftingIngredientSlotUI>();
            int haveCount = playerInventory != null ? playerInventory.GetItemCount(ingredient.itemData) : 0;
            slot.Bind(ingredient, haveCount);
        }
    }

    private void RefreshCraftButton()
    {
        if (craftButton == null || CraftingManager.Instance == null) return;

        bool isLocked  = CraftingManager.Instance.IsLocked(selectedRecipe);
        bool canCraft  = !isLocked && CraftingManager.Instance.CanCraft(selectedRecipe);

        craftButton.interactable = canCraft;

        if (craftButtonText != null)
        {
            if (isLocked)
                craftButtonText.text = "잠김";
            else if (canCraft)
                craftButtonText.text = "제작 [Space]";
            else
                craftButtonText.text = "재료 부족";
        }
    }

    private void RefreshStationHint()
    {
        if (stationHintText == null || selectedRecipe == null) return;

        if (CraftingManager.Instance == null) { stationHintText.text = string.Empty; return; }

        bool isLearned = CraftingManager.Instance.IsLearned(selectedRecipe);
        var req = selectedRecipe.requiredStation;

        if (req == CraftStation.None)
        {
            stationHintText.text = string.Empty;
        }
        else if (isLearned)
        {
            stationHintText.text = "해금됨";
            stationHintText.color = new Color(0.4f, 1f, 0.4f);
        }
        else
        {
            bool nearby = CraftingManager.Instance.GetNearbyStation() >= req;
            string stationName = req switch
            {
                CraftStation.Workbench => "작업대",
                CraftStation.Forge     => "용광로",
                _                      => req.ToString(),
            };
            stationHintText.text  = nearby ? string.Empty : $"⚠ {stationName} 필요";
            stationHintText.color = nearby ? Color.yellow : new Color(1f, 0.5f, 0.3f);
        }
    }
}

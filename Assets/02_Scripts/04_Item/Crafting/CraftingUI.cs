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
        RecipeCategory.Weapons,    // [2] 작업대 (무기+방어구, 보기 전용)
        RecipeCategory.Armor,      // [3] (숨김 — 작업대 탭에 합침)
        RecipeCategory.Magic,      // [4] 달 연성대 (마력 무기+의식, 보기 전용) — 생존 탭은 치료제에 합쳐 이 버튼을 재활용
        RecipeCategory.Structures, // [5] 구조물
        RecipeCategory.Medicine,   // [6] 치료제
        RecipeCategory.Cooking,    // [7] 요리솥 (요리, 보기 전용)
        RecipeCategory.All,        // [8] 전체
    };

    // 화면에 보이는 탭 순서 (Tab 키 순환도 이 순서). 방어구는 작업대 탭에 합쳐 숨긴다.
    private static readonly RecipeCategory[] TabOrder =
    {
        RecipeCategory.Tools,      // 도구
        RecipeCategory.Light,      // 광원
        RecipeCategory.Structures, // 구조물
        RecipeCategory.Medicine,   // 치료제
        RecipeCategory.Cooking,    // 요리솥
        RecipeCategory.Weapons,    // 작업대
        RecipeCategory.Magic,      // 달연성대
        RecipeCategory.All,        // 전체
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

        // Tab / Shift+Tab: 카테고리 전환 (방어구 탭은 숨김 — 작업대 탭에 합침)
        if (kb.tabKey.wasPressedThisFrame)
        {
            int dir = kb.shiftKey.isPressed ? -1 : 1;
            int pos = System.Array.IndexOf(TabOrder, selectedCategory);
            if (pos < 0) pos = 0;
            SelectCategory(TabOrder[(pos + dir + TabOrder.Length) % TabOrder.Length]);
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
        Extensions.PlaySFX(gameObject.activeSelf ? AudioLibrarySounds.UIOpen : AudioLibrarySounds.UIClose);
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
            var recipes = GetRecipes(category, showOnlyCraftable);
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
            var recipes = GetRecipes(selectedCategory, showOnlyCraftable);
            int idx = recipes.IndexOf(recipe);
            selectedRecipeIndex = idx >= 0 ? idx : 0;
        }

        RefreshRecipeList();
        RefreshDetail();
    }

    public void Craft()
    {
        if (selectedRecipe == null || CraftingManager.Instance == null) return;
        if (IsViewOnly(selectedCategory)) return; // 구조물 탭은 보기 전용 — 해당 구조물에서 E키로 제작
        CraftingManager.Instance.Craft(selectedRecipe);
    }

    public void ToggleFilter()
    {
        showOnlyCraftable   = !showOnlyCraftable;
        selectedRecipeIndex = 0;
        selectedRecipe      = null;

        if (CraftingManager.Instance != null)
        {
            var recipes = GetRecipes(selectedCategory, showOnlyCraftable);
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
        var recipes = GetRecipes(selectedCategory, showOnlyCraftable);
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
            var recipes = GetRecipes(selectedCategory, true);
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

            if (IsHiddenCategory(Categories[i]))
            {
                categoryTabButtons[i].gameObject.SetActive(false);
                continue;
            }

            // 구조물 탭: 이름을 구조물 이름으로 바꿔 보여준다 (제작은 그 구조물에서만 가능)
            if (IsViewOnly(Categories[i]))
            {
                var label = categoryTabButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = TabLabel(Categories[i]);
                    label.enableWordWrapping = false;          // 한 줄로
                    label.overflowMode = TextOverflowModes.Overflow;
                    label.enableAutoSizing = true;             // 탭 폭보다 길면 글자 크기를 줄여 맞춘다
                    label.fontSizeMax = label.fontSize;
                    label.fontSizeMin = Mathf.Max(8f, label.fontSize * 0.55f);
                }
            }

            int index = i;
            categoryTabButtons[i].onClick.AddListener(() => SelectCategory(Categories[index]));
        }

        ArrangeTabs();
    }

    /// <summary>탭 버튼을 TabOrder 순서로 배치한다. 같은 부모 아래 있을 때만 정렬한다.</summary>
    private void ArrangeTabs()
    {
        Transform parent = null;
        foreach (var b in categoryTabButtons)
        {
            if (b == null) continue;
            if (parent == null) parent = b.transform.parent;
            else if (b.transform.parent != parent) return;
        }
        if (parent == null) return;

        int sibling = 0;
        foreach (var category in TabOrder)
        {
            int idx = System.Array.IndexOf(Categories, category);
            if (idx < 0 || idx >= categoryTabButtons.Length || categoryTabButtons[idx] == null) continue;
            categoryTabButtons[idx].transform.SetSiblingIndex(sibling++);
        }
    }

    // 방어구는 작업대 탭에 합쳐서 탭 버튼을 숨긴다 (요리/무기/마력은 구조물 이름 탭으로 보여준다)
    private static bool IsHiddenCategory(RecipeCategory category) => category == RecipeCategory.Armor;

    /// <summary>해당 구조물에서만 제작할 수 있는 탭. 여기서는 레시피 재료를 보기만 하고 제작 버튼은 막는다.</summary>
    private static bool IsViewOnly(RecipeCategory category) =>
        category == RecipeCategory.Weapons || category == RecipeCategory.Magic || category == RecipeCategory.Cooking;

    /// <summary>탭 버튼에 쓰는 짧은 이름 (달 연성대는 공백을 빼서 한 줄에 들어가게)</summary>
    private static string TabLabel(RecipeCategory category) =>
        category == RecipeCategory.Magic ? "달연성대" : StationName(category);

    private static string StationName(RecipeCategory category) => category switch
    {
        RecipeCategory.Weapons => "작업대",
        RecipeCategory.Magic   => "달 연성대",
        RecipeCategory.Cooking => "요리솥",
        _                      => string.Empty,
    };

    /// <summary>탭에 보여줄 레시피. 구조물 탭은 관련 카테고리를 합치고, 재료 필터는 적용하지 않는다(제작 불가 탭이라).</summary>
    private List<RecipeDataSO> GetRecipes(RecipeCategory category, bool onlyCraftable)
    {
        var manager = CraftingManager.Instance;
        if (manager == null) return new List<RecipeDataSO>();

        bool viewOnly = IsViewOnly(category);
        var list = manager.GetRecipesByCategory(category, onlyCraftable && !viewOnly);
        if (category == RecipeCategory.Weapons)
            list.AddRange(manager.GetRecipesByCategory(RecipeCategory.Armor, false));
        else if (category == RecipeCategory.Magic)
            list.AddRange(manager.GetRecipesByCategory(RecipeCategory.Ritual, false));
        return list;
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

        var recipes = GetRecipes(selectedCategory, showOnlyCraftable);

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
                bool canCraft  = !IsViewOnly(selectedCategory) && CraftingManager.Instance.CanCraft(recipes[i]);
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

        if (IsViewOnly(selectedCategory))
        {
            craftButton.interactable = false;
            if (craftButtonText != null) craftButtonText.text = $"{StationName(selectedCategory)}에서 제작 가능";
            return;
        }

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

        if (IsViewOnly(selectedCategory))
        {
            stationHintText.text = string.Empty; // 안내는 제작 버튼 문구로 충분하다
            return;
        }

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
            bool nearby = CraftingManager.Instance.IsNearStation(req);
            string stationName = req switch
            {
                CraftStation.Workbench => "작업대",
                CraftStation.Forge     => "용광로",
                CraftStation.CookPot   => "화덕",
                _                      => req.ToString(),
            };
            stationHintText.text  = nearby ? string.Empty : $"{stationName} 필요";
            stationHintText.color = nearby ? Color.yellow : new Color(1f, 0.5f, 0.3f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 크래프팅 패널 전체 관리
/// C키로 열고 닫기
/// </summary>

public class CraftingUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject craftingSlotPrefab;
    [SerializeField] private Transform recipeContainer;

    [Header("카테고리 탭 버튼")]
    [SerializeField] private Button tabTools;
    [SerializeField] private Button tabLight;
    [SerializeField] private Button tabSurvival;
    [SerializeField] private Button tabWeapons;
    [SerializeField] private Button tabMoon;

    [Header("열기 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.C;

    private List<CraftingSlotUI> _slotUIs = new List<CraftingSlotUI>();
    private RecipeCategory _currentCategory = RecipeCategory.Tools;
    private bool _isOpen = false;

    private void Start()
    {
        // 탭 버튼 이벤트 연결
        tabTools.onClick.AddListener(() => ShowCategory(RecipeCategory.Tools));
        tabLight.onClick.AddListener(() => ShowCategory(RecipeCategory.Light));
        tabSurvival.onClick.AddListener(() => ShowCategory(RecipeCategory.Survival));
        tabWeapons.onClick.AddListener(() => ShowCategory(RecipeCategory.Weapons));
        tabMoon.onClick.AddListener(() => ShowCategory(RecipeCategory.Moon));

        // 시작 시 닫힘 (크기를 0으로)
        gameObject.SetActive(true);
        GetComponent<CanvasGroup>().alpha = 0;
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

/*    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }*/

    public void OpenDefault()
    {
        ShowCategory(_currentCategory);
    }

    private void Toggle()
    {
        _isOpen = !_isOpen;
        var cg = GetComponent<CanvasGroup>();
        cg.alpha = _isOpen ? 1 : 0;
        cg.blocksRaycasts = _isOpen;
        if (_isOpen) ShowCategory(_currentCategory);
    }


    private void ShowCategory(RecipeCategory category)
    {
        _currentCategory = category;

        // 기존 슬롯 제거
        foreach (Transform child in recipeContainer)
            Destroy(child.gameObject);
        _slotUIs.Clear();

        // 카테고리 레시피 가져오기
        var recipes = CraftingManager.Instance.GetRecipesByCategory(category);

        // 슬롯 생성
        foreach (var recipe in recipes)
        {
            var go = Instantiate(craftingSlotPrefab, recipeContainer);
            var slot = go.GetComponent<CraftingSlotUI>();
            slot.SetRecipe(recipe);
            _slotUIs.Add(slot);
        }
    }
}
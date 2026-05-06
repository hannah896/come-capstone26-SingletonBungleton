using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 레시피 슬롯 하나
/// </summary>
public class CraftingSlotUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private Button craftButton;
    [SerializeField] private Image craftButtonImage;

    private RecipeDataSO _recipe;

    private void Start()
    {
        craftButton.onClick.AddListener(OnCraftClicked);
        CraftingManager.Instance.OnCraftingChanged += Refresh;
        InventoryManager.Instance.OnInventoryChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (CraftingManager.Instance != null)
            CraftingManager.Instance.OnCraftingChanged -= Refresh;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }

    public void SetRecipe(RecipeDataSO recipe)
    {
        _recipe = recipe;
        Refresh();
    }

    private void Refresh()
    {
        if (_recipe == null) return;
        recipeNameText.text = _recipe.recipeName;  //이름

        if (_recipe.icon != null)   //아이콘
        {
            iconImage.enabled = true;
            iconImage.sprite = _recipe.icon;
        }
        else
            iconImage.enabled = false;

        // 제작 가능=초록,제작불가=회색
        bool canCraft = CraftingManager.Instance.CanCraft(_recipe);
        craftButtonImage.color = canCraft ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
        craftButton.interactable = canCraft;
    }

    private void OnCraftClicked()
    {
        if (_recipe == null) return;
        CraftingManager.Instance.Craft(_recipe);
    }
}
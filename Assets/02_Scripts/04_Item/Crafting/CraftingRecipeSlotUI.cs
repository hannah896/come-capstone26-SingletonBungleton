using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CraftingRecipeSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image background;

    private static readonly Color ColorSelected    = new(1f,   0.85f, 0.25f, 1f);
    private static readonly Color ColorCraftable   = new(0.9f, 0.9f,  0.9f,  1f);
    private static readonly Color ColorUncraftable = new(0.45f, 0.45f, 0.45f, 1f);

    public event Action<RecipeDataSO> OnSelected;
    private RecipeDataSO recipe;

    public void Bind(RecipeDataSO recipe, bool canCraft, bool isSelected)
    {
        this.recipe = recipe;

        if (iconImage != null)
        {
            iconImage.sprite  = recipe?.resultItem?.icon;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
            nameText.text = recipe?.recipeName ?? string.Empty;

        if (background != null)
            background.color = isSelected ? ColorSelected : canCraft ? ColorCraftable : ColorUncraftable;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnSelected?.Invoke(recipe);
    }
}

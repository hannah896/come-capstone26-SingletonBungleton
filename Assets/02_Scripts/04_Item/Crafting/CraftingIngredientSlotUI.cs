using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingIngredientSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    private static readonly Color ColorSufficient   = Color.white;
    private static readonly Color ColorInsufficient = new(1f, 0.35f, 0.35f, 1f);

    public void Bind(RecipeIngredient ingredient, int haveCount)
    {
        ItemDataSO itemData = ingredient.itemData;
        int required = ingredient.amount;

        if (iconImage != null)
        {
            iconImage.sprite  = itemData?.icon;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
            nameText.text = itemData?.itemName ?? string.Empty;

        if (countText != null)
        {
            countText.text  = $"{haveCount}/{required}";
            countText.color = haveCount >= required ? ColorSufficient : ColorInsufficient;
        }
    }
}

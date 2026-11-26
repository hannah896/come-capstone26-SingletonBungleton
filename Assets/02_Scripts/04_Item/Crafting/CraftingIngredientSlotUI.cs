using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingIngredientSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    // 이름 칸은 두 줄 높이가 안 되어서, 슬롯이 좁아지면(재료가 많을 때) 긴 이름이 '...'로 잘렸다.
    // 슬롯 폭에 맞게 글자 크기를 자동으로 줄여 이름이 다 보이게 한다.
    private const float NameMinFontSize = 10f;
    private float nameBaseFontSize;

    private void FitNameText()
    {
        if (nameBaseFontSize <= 0f) nameBaseFontSize = nameText.fontSize;

        nameText.enableAutoSizing = true;
        nameText.fontSizeMax = nameBaseFontSize;
        nameText.fontSizeMin = NameMinFontSize;
        nameText.textWrappingMode = TextWrappingModes.Normal;
    }

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
        {
            nameText.text = itemData?.itemName ?? string.Empty;
            FitNameText();
        }

        if (countText != null)
        {
            countText.text  = $"{haveCount}/{required}";
            countText.color = haveCount >= required ? ColorSufficient : ColorInsufficient;
        }
    }
}

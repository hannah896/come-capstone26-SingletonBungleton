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
    [SerializeField] private GameObject lockOverlay;    // 자물쇠 아이콘 오브젝트 (선택)
    [SerializeField] private GameObject learnedBadge;  // 해금 뱃지 오브젝트 (선택)

    // Don't Starve 팔레트 (빌더의 DS 색상과 일치)
    private static readonly Color ColorSelected    = new Color32(202, 136,  18, 255);  // 호박색
    private static readonly Color ColorCraftable   = new Color32( 60,  44,  20, 255);  // 제작 가능 갈색
    private static readonly Color ColorUncraftable = new Color32( 38,  27,  15, 255);  // 제작 불가 어두운 갈색
    private static readonly Color ColorLocked      = new Color32( 14,   9,   5, 255);  // 잠금 거의 검정

    public event Action<RecipeDataSO> OnSelected;
    private RecipeDataSO recipe;

    /// <param name="isLocked">스테이션 없고 미해금 → 자물쇠 표시</param>
    /// <param name="isLearned">이전에 프로토타입 완료 → 해금 뱃지</param>
    public void Bind(RecipeDataSO recipe, bool canCraft, bool isSelected, bool isLocked = false, bool isLearned = false)
    {
        this.recipe = recipe;

        if (iconImage != null)
        {
            iconImage.sprite  = recipe?.resultItem?.icon;
            iconImage.enabled = iconImage.sprite != null;
            // 잠금 시 반투명 회색 (lockOverlay가 추가로 어둡게 처리함)
            iconImage.color   = isLocked ? new Color(0.3f, 0.3f, 0.3f, 0.6f) : Color.white;
        }

        if (nameText != null)
        {
            nameText.text  = recipe?.recipeName ?? string.Empty;
            nameText.color = isLocked ? ColorLocked : Color.white;
        }

        if (background != null)
        {
            if (isSelected)       background.color = ColorSelected;
            else if (isLocked)    background.color = ColorLocked;
            else if (canCraft)    background.color = ColorCraftable;
            else                  background.color = ColorUncraftable;
        }

        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        // 해금 뱃지: 스테이션 레시피이고 이미 배웠을 때만 표시
        if (learnedBadge != null)
            learnedBadge.SetActive(isLearned && recipe != null && recipe.requiredStation != CraftStation.None);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        OnSelected?.Invoke(recipe);
    }
}

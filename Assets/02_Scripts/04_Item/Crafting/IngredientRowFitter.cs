using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제작 팝업 아래쪽 '필요 재료' 줄이 팝업 가로폭을 넘지 않도록 슬롯 너비를 줄여 맞춘다.
/// 재료 슬롯은 프리팹에 고정 너비(124)라서 재료가 4개 이상이면 줄이 팝업 밖으로 넘쳤다.
/// 컨테이너가 HorizontalLayoutGroup이고 자식 너비를 직접 두는 경우(childControlWidth == false)에 맞춘다.
/// </summary>
public static class IngredientRowFitter
{
    private const float MinSlotWidth = 56f;

    public static void Fit(Transform container, IReadOnlyList<GameObject> slots)
    {
        if (container == null || slots == null || slots.Count == 0) return;

        var layout = container.GetComponent<HorizontalLayoutGroup>();
        var rect = container as RectTransform;
        if (layout == null || rect == null || layout.childControlWidth) return;

        // 팝업이 막 열린 직후라 크기가 아직 계산되지 않았을 수 있다
        Canvas.ForceUpdateCanvases();
        float width = rect.rect.width;
        if (width <= 1f && rect.parent is RectTransform parent) width = parent.rect.width;
        if (width <= 1f) return;

        int count = slots.Count;
        float available = width - layout.padding.horizontal - layout.spacing * (count - 1);
        float slotWidth = Mathf.Max(MinSlotWidth, available / count);

        foreach (GameObject slot in slots)
        {
            if (slot == null || !slot.TryGetComponent(out RectTransform slotRect)) continue;
            if (slotRect.sizeDelta.x <= slotWidth) continue; // 이미 충분히 좁으면 그대로

            slotRect.sizeDelta = new Vector2(slotWidth, slotRect.sizeDelta.y);
        }
    }
}

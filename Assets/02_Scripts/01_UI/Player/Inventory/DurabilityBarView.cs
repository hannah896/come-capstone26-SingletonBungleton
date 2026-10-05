using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯 하단에 뜨는 내구도 바. 인벤토리 슬롯과 보관함 슬롯이 같이 쓴다.
/// 슬롯 프리팹에 바 오브젝트가 없으면 직접 만들고, 배경 안에서 가로로 줄어드는 채움 이미지를 쓴다
/// (스프라이트 없는 Image는 fillAmount가 안 먹는다).
/// </summary>
public class DurabilityBarView
{
    private const string BarName = "durabilityBar";
    private const string FillName = "durabilityFill";

    private static readonly Color High = new(0.3f, 0.8f, 0.35f, 1f);
    private static readonly Color Mid = new(0.95f, 0.8f, 0.2f, 1f);
    private static readonly Color Low = new(0.9f, 0.25f, 0.2f, 1f);

    private readonly Transform slot;
    private Image bar;
    private Image fill;
    private float lastPercent = -2f;

    /// <param name="existingBar">슬롯 프리팹에 이미 있는 바. 없으면 null.</param>
    public DurabilityBarView(Transform slot, Image existingBar = null)
    {
        this.slot = slot;
        bar = existingBar;
    }

    /// <summary>percent는 0~1. 음수면 내구도가 없는 아이템이므로 바를 숨긴다.</summary>
    public void Set(float percent)
    {
        if (percent < 0f)
        {
            Hide();
            return;
        }

        EnsureBar();
        if (!bar.gameObject.activeSelf)
            bar.gameObject.SetActive(true);

        if (Mathf.Approximately(percent, lastPercent)) return;
        lastPercent = percent;

        fill.rectTransform.anchorMax = new Vector2(percent, 1f);
        fill.color = percent > 0.5f
            ? Color.Lerp(Mid, High, (percent - 0.5f) * 2f)
            : Color.Lerp(Low, Mid, percent * 2f);
    }

    public void Hide()
    {
        lastPercent = -2f;
        if (bar != null)
            bar.gameObject.SetActive(false);
    }

    private void EnsureBar()
    {
        if (bar == null)
        {
            var go = new GameObject(BarName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(slot, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.1f, 0f);
            rect.anchorMax = new Vector2(0.9f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, 4f);
            rect.offsetMax = new Vector2(0f, 10f);

            bar = go.GetComponent<Image>();
        }

        if (fill != null) return;

        Transform existing = bar.transform.Find(FillName);
        if (existing != null)
            fill = existing.GetComponent<Image>();

        if (fill == null)
        {
            var go = new GameObject(FillName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(bar.transform, false);
            fill = go.GetComponent<Image>();
        }

        bar.color = new Color(0f, 0f, 0f, 0.7f);
        bar.raycastTarget = false;
        fill.raycastTarget = false;

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }
}

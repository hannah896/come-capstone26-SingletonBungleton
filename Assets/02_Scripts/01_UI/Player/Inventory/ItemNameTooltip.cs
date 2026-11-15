using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯 위에 마우스를 올리면 아이템 이름을 마우스 옆에 보여주는 작은 말풍선.
/// 프리팹 없이 코드로 만들고, 마우스를 따라다닌다. 화면 가장자리에서는 반대쪽으로 뒤집혀 잘리지 않는다.
/// </summary>
public static class ItemNameTooltip
{
    private static GameObject root;
    private static RectTransform rect;
    private static TextMeshProUGUI label;
    private static ItemNameTooltipFollower follower;

    /// <summary>이름을 보여준다. 빈 문자열이면 숨긴다. font는 슬롯 텍스트의 한글 폰트를 넘겨 받는다.</summary>
    public static void Show(string text, TMP_FontAsset font)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        Ensure(font);
        label.text = text;
        root.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        follower.Follow();
    }

    public static void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    /// <summary>게임 전체가 쓰는 한글 폰트(FontsSo). 슬롯 숫자 텍스트의 폰트는 한글 글리프가 없어 □로 깨진다.</summary>
    private static TMP_FontAsset ResolveKoreanFont(TMP_FontAsset fallback)
    {
        var so = Main.Text != null ? Main.Text.FontSo : null;
        if (so != null && so.dictFontsData != null)
        {
            foreach (var kv in so.dictFontsData)
                if (kv.Value != null) return kv.Value;
        }
        return fallback;
    }

    private static void Ensure(TMP_FontAsset font)
    {
        font = ResolveKoreanFont(font);

        if (root != null)
        {
            if (font != null && label.font != font) label.font = font;
            return;
        }

        var canvasGo = new GameObject("ItemNameTooltipCanvas", typeof(Canvas));
        Object.DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        root = new GameObject("Tooltip", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(canvasGo.transform, false);
        rect = (RectTransform)root.transform;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = rect.anchorMax = Vector2.zero;

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.04f, 0.03f, 0.92f);
        bg.raycastTarget = false;

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = root.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(root.transform, false);
        label = textGo.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = 24f;
        label.color = new Color(1f, 0.93f, 0.78f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        follower = root.AddComponent<ItemNameTooltipFollower>();
        follower.Init(rect, canvas);
        root.SetActive(false);
    }
}

/// <summary>말풍선이 보이는 동안 마우스를 따라다닌다.</summary>
public class ItemNameTooltipFollower : MonoBehaviour
{
    private static readonly Vector2 Offset = new(18f, -18f);

    private RectTransform rect;
    private Canvas canvas;

    public void Init(RectTransform target, Canvas ownerCanvas)
    {
        rect = target;
        canvas = ownerCanvas;
    }

    private void LateUpdate() => Follow();

    public void Follow()
    {
        if (rect == null || Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 size = rect.sizeDelta;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float width = size.x * scale;
        float height = size.y * scale;

        Vector2 pos = mouse + Offset * scale;
        // 오른쪽/아래 가장자리를 넘으면 마우스 반대쪽으로 뒤집는다
        if (pos.x + width > Screen.width) pos.x = mouse.x - Offset.x * scale - width;
        if (pos.y - height < 0f) pos.y = mouse.y + height - Offset.y * scale;

        rect.position = pos;
    }
}

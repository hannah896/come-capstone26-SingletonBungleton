using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기존 UI 스타일을 재사용하는 저장/이어하기 버튼.</summary>
public static class SaveMenuButton
{
    public static UI_Button Add(UI_Button template, Transform root, string objectName, string label, UnityAction action)
    {
        if (template == null) return null;
        Transform parent = template.transform.parent;
        bool hasLayout = parent.GetComponent<LayoutGroup>() != null;
        var button = Object.Instantiate(template, hasLayout ? parent : root);
        button.name = objectName;
        button.transform.SetAsFirstSibling();
        var trigger = button.GetComponent<EventTrigger>();
        if (trigger != null) trigger.triggers.Clear();
        button.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        button.SetEvent(action);
        foreach (var text in button.GetComponentsInChildren<UI_Text>(true))
        {
            text.LocaleName = ELocalizedName.NONE;
            text.Text = label;
        }
        if (!hasLayout)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.12f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300f, 70f);
            rect.localScale = Vector3.one;
            button.transform.SetAsLastSibling();
        }
        return button;
    }
}

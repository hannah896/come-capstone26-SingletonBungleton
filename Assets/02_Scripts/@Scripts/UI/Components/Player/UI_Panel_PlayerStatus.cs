using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_Panel_PlayerStatus : UI_Panel
{
    private enum StatusKind
    {
        Hunger,
        Hp,
        Ego
    }

    [SerializeField] private UI_Image UI_HungerIcon;
    [SerializeField] private UI_Image UI_HPIcon;
    [SerializeField] private UI_Image UI_EgoIcon;
    [SerializeField] private UI_Text UI_HungerText;
    [SerializeField] private UI_Text UI_HPText;
    [SerializeField] private UI_Text UI_EgoText;

    private PlayerStatus status;
    private StatusKind? hoveredKind;
    private LoopManager subscribedLoop;
    private Player player;
    private bool isLoopSubscribed;

    public Player Player
    {
        get => player;
        set
        {
            player = value;
            status = player != null ? player.Stat : null;
            RefreshAllValues();
            HideAllValues();
        }
    }

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        ResolveReferences();
        SetIconEvent(UI_HungerIcon, StatusKind.Hunger);
        SetIconEvent(UI_HPIcon, StatusKind.Hp);
        SetIconEvent(UI_EgoIcon, StatusKind.Ego);
        HideAllValues();
        TrySubscribeLoop();

        hoveredKind = null;
        RefreshAllValues();
        HideAllValues();
        return true;
    }

    private void OnEnable()
    {
        TrySubscribeLoop();
    }

    private void OnDisable()
    {
        UnsubscribeLoop();
        hoveredKind = null;
        HideAllValues();
    }

    private void OnLoopUpdate(float deltaTime)
    {
        if (player != null)
            status = player.Stat;

        if (hoveredKind.HasValue)
            RefreshValue(hoveredKind.Value);
    }

    private void ResolveReferences()
    {
        UI_HungerIcon ??= gameObject.FindChild<UI_Image>("UI_HungerIcon");
        UI_HPIcon ??= gameObject.FindChild<UI_Image>("UI_HPIcon");
        UI_EgoIcon ??= gameObject.FindChild<UI_Image>("UI_EgoIcon");
        UI_HungerText ??= gameObject.FindChild<UI_Text>("UI_HungerText");
        UI_HPText ??= gameObject.FindChild<UI_Text>("UI_HPText");
        UI_EgoText ??= gameObject.FindChild<UI_Text>("UI_EgoText");

        DisableTextRaycast(UI_HungerText);
        DisableTextRaycast(UI_HPText);
        DisableTextRaycast(UI_EgoText);
    }

    private void SetIconEvent(UI_Image icon, StatusKind kind)
    {
        if (icon == null) return;

        icon.Initialize();
        if (icon.Image != null)
            icon.Image.raycastTarget = true;

        EventTrigger trigger = icon.gameObject.GetOrAddComponent<EventTrigger>();
        AddEvent(trigger, EventTriggerType.PointerEnter, _ => ShowValue(kind));
        AddEvent(trigger, EventTriggerType.PointerExit, _ => HideValue(kind));
    }

    private void ShowValue(StatusKind kind)
    {
        hoveredKind = kind;
        RefreshValue(kind);
        SetValueVisible(kind, true);
    }

    private void HideValue(StatusKind kind)
    {
        if (hoveredKind == kind)
            hoveredKind = null;

        SetValueVisible(kind, false);
    }

    private void RefreshValue(StatusKind kind)
    {
        if (status == null)
        {
            SetText(kind, "--/--");
            return;
        }

        switch (kind)
        {
            case StatusKind.Hunger:
                SetText(kind, FormatValue(status.CurrentHunger, status.MaxHunger));
                break;
            case StatusKind.Hp:
                SetText(kind, FormatValue(status.CurrentHp, status.MaxHp));
                break;
            case StatusKind.Ego:
                SetText(kind, FormatValue(status.CurrentEgo, status.MaxEgo));
                break;
        }
    }

    private void RefreshAllValues()
    {
        RefreshValue(StatusKind.Hunger);
        RefreshValue(StatusKind.Hp);
        RefreshValue(StatusKind.Ego);
    }

    private void SetText(StatusKind kind, string value)
    {
        UI_Text text = GetText(kind);
        if (text != null)
            text.Text = value;
    }

    private void SetValueVisible(StatusKind kind, bool visible)
    {
        UI_Text text = GetText(kind);
        if (text != null)
            SetTextVisible(text, visible);
    }

    private void HideAllValues()
    {
        SetValueVisible(StatusKind.Hunger, false);
        SetValueVisible(StatusKind.Hp, false);
        SetValueVisible(StatusKind.Ego, false);
    }

    private UI_Text GetText(StatusKind kind)
    {
        return kind switch
        {
            StatusKind.Hunger => UI_HungerText,
            StatusKind.Hp => UI_HPText,
            StatusKind.Ego => UI_EgoText,
            _ => null
        };
    }

    private static void DisableTextRaycast(UI_Text text)
    {
        if (text == null) return;

        text.Initialize();
        if (text.TMP != null)
            text.TMP.raycastTarget = false;
    }

    private static void SetTextVisible(UI_Text text, bool visible)
    {
        text.gameObject.SetActive(visible);
    }

    private static void AddEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new() { eventID = eventType };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void TrySubscribeLoop()
    {
        LoopManager loop = Main.Loop;
        if (isLoopSubscribed || loop == null) return;

        loop.OnUpdate += OnLoopUpdate;
        subscribedLoop = loop;
        isLoopSubscribed = true;
    }

    private void UnsubscribeLoop()
    {
        if (!isLoopSubscribed) return;

        if (subscribedLoop != null)
            subscribedLoop.OnUpdate -= OnLoopUpdate;

        subscribedLoop = null;
        isLoopSubscribed = false;
    }

    private static string FormatValue(float current, float max)
        => $"{current:0.#}/{max:0.#}";
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 팝업 UI입니다.
/// Addressables 키는 기본적으로 "UI_Popup_Inventory"를 사용합니다.
/// </summary>
public class UI_Popup_Inventory : UI_Popup
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotContainer;

    private readonly List<InventorySlotUI> slotUIs = new();
    private bool isRuntimeFallback;

    public static UI_Popup_Inventory CreateRuntimeFallback(Transform parent)
    {
        var go = new GameObject("UI_Popup_Inventory_Runtime", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var popup = go.AddComponent<UI_Popup_Inventory>();
        popup.isRuntimeFallback = true;
        return popup;
    }

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        InventoryManager.EnsureInstance();
        EnsureRuntimeLayout();
        BuildUI();
        InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        return true;
    }

    public override void Close()
    {
        if (!isRuntimeFallback)
        {
            base.Close();
            return;
        }

        if (_onClose) return;
        _onClose = true;
        OnCloseEvent?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;

        OnDestroyEvent?.Invoke();
    }

    private void BuildUI()
    {
        if (slotPrefab == null || slotContainer == null)
        {
            Debug.LogWarning("[InventoryUI] SlotPrefab 또는 SlotContainer가 연결되지 않았습니다.");
            return;
        }

        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);

        slotUIs.Clear();

        for (int i = 0; i < InventoryManager.Instance.slots.Count; i++)
        {
            GameObject go = Instantiate(slotPrefab, slotContainer);
            slotUIs.Add(go.GetComponent<InventorySlotUI>());
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (InventoryManager.Instance == null) return;

        List<ItemDataSO> slots = InventoryManager.Instance.slots;
        List<int> stacks = InventoryManager.Instance.stackCounts;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;
            slotUIs[i].Refresh(i, slots[i], stacks[i]);
        }
    }

    private void EnsureRuntimeLayout()
    {
        if (slotPrefab != null && slotContainer != null) return;

        var background = CreateUIObject("Background", transform);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(background);

        var panel = CreateUIObject("InventoryPanel", transform);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(620f, 420f);
        panel.anchoredPosition = Vector2.zero;

        var panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.1f, 0.96f);

        var grid = CreateUIObject("SlotContainer", panel);
        grid.anchorMin = new Vector2(0f, 0f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.offsetMin = new Vector2(24f, 24f);
        grid.offsetMax = new Vector2(-24f, -24f);

        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(96f, 96f);
        layout.spacing = new Vector2(10f, 10f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;

        slotContainer = grid;
        slotPrefab = CreateSlotPrefab(transform);
    }

    private static GameObject CreateSlotPrefab(Transform parent)
    {
        var slot = CreateUIObject("RuntimeInventorySlotPrefab", parent);
        slot.gameObject.SetActive(false);

        var slotImage = slot.gameObject.AddComponent<Image>();
        slotImage.color = new Color(0.16f, 0.17f, 0.18f, 1f);

        var icon = CreateUIObject("iconImage", slot);
        Stretch(icon, 14f);
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.enabled = false;

        var stackBg = CreateUIObject("stackBG", slot);
        stackBg.anchorMin = new Vector2(1f, 0f);
        stackBg.anchorMax = new Vector2(1f, 0f);
        stackBg.pivot = new Vector2(1f, 0f);
        stackBg.sizeDelta = new Vector2(38f, 24f);
        stackBg.anchoredPosition = new Vector2(-6f, 6f);
        var stackBgImage = stackBg.gameObject.AddComponent<Image>();
        stackBgImage.color = new Color(0f, 0f, 0f, 0.65f);

        var stackText = CreateUIObject("stackText", stackBg);
        Stretch(stackText);
        var text = stackText.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontSize = 18f;
        text.color = Color.white;

        var durability = CreateUIObject("durabilityBar", slot);
        durability.anchorMin = new Vector2(0f, 0f);
        durability.anchorMax = new Vector2(1f, 0f);
        durability.pivot = new Vector2(0.5f, 0f);
        durability.offsetMin = new Vector2(8f, 6f);
        durability.offsetMax = new Vector2(-8f, 12f);
        var durabilityImage = durability.gameObject.AddComponent<Image>();
        durabilityImage.color = new Color(0.3f, 0.8f, 0.35f, 1f);
        durability.gameObject.SetActive(false);

        slot.gameObject.AddComponent<InventorySlotUI>();
        return slot.gameObject;
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }
}

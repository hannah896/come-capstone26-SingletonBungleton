using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 연결")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private GameObject stackBG;
    [SerializeField] private Image durabilityBar;
    [SerializeField] private Outline focusOutline;

    private int slotIndex;
    private PlayerInventory playerInventory;
    private EquipSlot equipSlot = EquipSlot.None;
    private bool isEquipmentSlot;
    private bool referencesResolved;

    private void Awake()
    {
        ResolveReferences();
    }

    public void BindInventorySlot(PlayerInventory inventory, int index)
    {
        playerInventory = inventory;
        slotIndex = index;
        equipSlot = EquipSlot.None;
        isEquipmentSlot = false;
    }

    public void BindEquipmentSlot(PlayerInventory inventory, EquipSlot slot)
    {
        playerInventory = inventory;
        slotIndex = -1;
        equipSlot = slot;
        isEquipmentSlot = true;
    }

    public void Refresh(int index, ItemDataSO itemData, int stack)
    {
        ResolveReferences();
        slotIndex = index;
        isEquipmentSlot = false;

        RefreshItem(itemData, stack, true);
    }

    public void RefreshEquipment(EquipSlot slot, ItemDataSO itemData)
    {
        ResolveReferences();
        equipSlot = slot;
        slotIndex = -1;
        isEquipmentSlot = true;

        RefreshItem(itemData, 1, false);
    }

    public void SetFocused(bool focused)
    {
        ResolveReferences();

        if (focusOutline == null)
        {
            focusOutline = gameObject.GetComponent<Outline>();
            if (focusOutline == null)
                focusOutline = gameObject.AddComponent<Outline>();

            focusOutline.effectColor = new Color(1f, 0.82f, 0.22f, 1f);
            focusOutline.effectDistance = new Vector2(4f, -4f);
            focusOutline.useGraphicAlpha = false;
        }

        focusOutline.enabled = focused;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playerInventory == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (!isEquipmentSlot)
                playerInventory.EatFromSlot(slotIndex);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (isEquipmentSlot)
        {
            playerInventory.UnequipItem(equipSlot);
            return;
        }

        playerInventory.EquipFromSlot(slotIndex);
    }

    private void RefreshItem(ItemDataSO itemData, int stack, bool showStackCount)
    {
        if (itemData == null)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            bool hasIcon = itemData.icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            iconImage.enabled = hasIcon;
            iconImage.sprite = itemData.icon;
        }

        bool showStack = showStackCount && itemData.isStackable && stack > 1;
        if (stackBG != null) stackBG.SetActive(showStack);
        if (stackText != null)
        {
            stackText.gameObject.SetActive(showStack);
            stackText.text = showStack ? stack.ToString() : string.Empty;
        }

        if (durabilityBar != null)
            durabilityBar.gameObject.SetActive(itemData.hasDurability);
    }

    private void Clear()
    {
        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
            iconImage.enabled = false;
            iconImage.sprite = null;
        }

        if (stackBG != null) stackBG.SetActive(false);
        if (stackText != null) stackText.text = string.Empty;
        if (durabilityBar != null) durabilityBar.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (referencesResolved)
        {
            if (focusOutline == null)
                focusOutline = GetComponent<Outline>();

            return;
        }

        if (HasRequiredReferences())
        {
            if (focusOutline == null)
                focusOutline = GetComponent<Outline>();

            referencesResolved = true;
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);

        if (iconImage == null)
            iconImage = FindChildComponent<Image>(children, "iconImage");
        if (iconImage == null)
            iconImage = FindChildComponent<Image>(children, "UI_Slot_Icon");
        if (stackText == null)
            stackText = FindChildComponent<TextMeshProUGUI>(children, "stackText");
        if (stackText == null)
            stackText = FindChildComponent<TextMeshProUGUI>(children, "txtButton");
        if (stackBG == null)
            stackBG = FindChild(children, "stackBG")?.gameObject;
        if (durabilityBar == null)
            durabilityBar = FindChildComponent<Image>(children, "durabilityBar");
        if (focusOutline == null)
            focusOutline = GetComponent<Outline>();

        referencesResolved = true;
    }

    private bool HasRequiredReferences()
    {
        return iconImage != null
            && stackText != null
            && stackBG != null
            && durabilityBar != null;
    }

    private T FindChildComponent<T>(Transform[] children, string childName) where T : Component
    {
        Transform child = FindChild(children, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform[] children, string childName)
    {
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == childName)
                return children[i];

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        referencesResolved = false;
        ResolveReferences();
    }
#endif
}

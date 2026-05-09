using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private GameObject stackBG;
    [SerializeField] private Image durabilityBar;

    private int slotIndex;

    public void Refresh(int index, ItemDataSO itemData, int stack)
    {
        ResolveReferences();
        slotIndex = index;

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

        bool showStack = itemData.isStackable && stack > 1;
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
        if (iconImage == null)
            iconImage = FindChildComponent<Image>("iconImage");
        if (iconImage == null)
            iconImage = FindChildComponent<Image>("UI_Slot_Icon");
        if (stackText == null)
            stackText = FindChildComponent<TextMeshProUGUI>("stackText");
        if (stackText == null)
            stackText = FindChildComponent<TextMeshProUGUI>("txtButton");
        if (stackBG == null)
            stackBG = FindChild("stackBG")?.gameObject;
        if (durabilityBar == null)
            durabilityBar = FindChildComponent<Image>("durabilityBar");
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = FindChild(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == childName)
                return children[i];

        return null;
    }
}

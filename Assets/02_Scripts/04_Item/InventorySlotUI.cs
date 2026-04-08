using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private GameObject stackBG;
    [SerializeField] private Image durabilityBar;

    private int _slotIndex;

    public void Refresh(int index, ItemDataSO itemData, int stack)
    {
        _slotIndex = index;

        if (itemData == null)
        {
            iconImage.enabled = false;
            stackBG.SetActive(false);
            if (durabilityBar) durabilityBar.gameObject.SetActive(false);
            return;
        }

        // 아이콘
        iconImage.enabled = true;
        if (itemData.icon != null)
            iconImage.sprite = itemData.icon;

        // 스택 수 (2개 이상일 때만 표시)
        bool showStack = itemData.isStackable && stack > 1;
        stackBG.SetActive(showStack);
        if (showStack) stackText.text = stack.ToString();

        // 내구도 바 (도구/장비만)
        if (durabilityBar)
            durabilityBar.gameObject.SetActive(itemData.hasDurability);
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상자, 요리솥, 제단, 가방 등 1칸 슬롯 등 저장소와 관련된 슬롯 1칸에 대한 UI를 나타내는 클래스.
/// 클릭하면 주인 팝업에 "이 칸을 눌렀다"고 알리고, 실제 이송은 팝업이 처리한다.
/// </summary>
public class StorageSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI stackText;
    [SerializeField] private GameObject stackBG;

    private UI_Popup_Chest owner;
    private int slotIndex = -1;

    public void Bind(UI_Popup_Chest popup, int index)
    {
        owner = popup;
        slotIndex = index;
    }

    /// <summary>슬롯 내용을 갱신한다. itemData가 null이면 빈 칸으로 표시.</summary>
    public void Refresh(ItemDataSO itemData, int stack)
    {
        if (itemData == null || stack <= 0)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = itemData.icon;
            iconImage.enabled = itemData.icon != null;
            iconImage.color = Color.white;
        }

        bool showStack = stack > 1;
        if (stackText != null)
        {
            stackText.text = showStack ? stack.ToString() : string.Empty;
            stackText.enabled = showStack;
        }
        if (stackBG != null) stackBG.SetActive(showStack);
    }

    private void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (stackText != null)
        {
            stackText.text = string.Empty;
            stackText.enabled = false;
        }
        if (stackBG != null) stackBG.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (owner == null || slotIndex < 0) return;

        owner.OnStorageSlotClicked(slotIndex);
    }
}

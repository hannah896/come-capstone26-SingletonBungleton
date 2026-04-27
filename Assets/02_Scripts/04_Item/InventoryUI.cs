using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 팝업 UI.
/// UIManager PopupLayer에 올라가며, GameScene에서 Tab 키로 열고 닫는다.
/// 프리팹을 Addressables에 "UI_Popup_Inventory" 키로 등록 필요 (에디터 작업).
/// </summary>
public class UI_Popup_Inventory : UI_Popup
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotContainer;

    private List<InventorySlotUI> _slotUIs = new();

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        BuildUI();
        InventoryManager.Instance.OnInventoryChanged += RefreshUI;
        return true;
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    private void BuildUI()
    {
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);
        _slotUIs.Clear();

        for (int i = 0; i < InventoryManager.Instance.slots.Count; i++)
        {
            var go = Instantiate(slotPrefab, slotContainer);
            _slotUIs.Add(go.GetComponent<InventorySlotUI>());
        }
        RefreshUI();
    }

    private void RefreshUI()
    {
        var slots = InventoryManager.Instance.slots;
        var stacks = InventoryManager.Instance.stackCounts;
        for (int i = 0; i < _slotUIs.Count; i++)
            _slotUIs[i].Refresh(i, slots[i], stacks[i]);
    }
}

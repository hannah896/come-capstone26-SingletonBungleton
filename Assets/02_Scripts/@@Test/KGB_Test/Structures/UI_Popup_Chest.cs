using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상자·냉장고를 열었을 때 나타나는 팝업 UI.
/// 플레이어 인벤토리는 항상 화면에 떠 있으므로 이 팝업은 보관함 칸만 보여준다.
///
/// 클릭 이송:
///   - 보관함 칸 클릭 → 그 스택을 인벤토리로
///   - 인벤토리 칸 클릭 → 그 스택을 보관함으로 (InventorySlotUI가 Current를 보고 호출한다)
/// </summary>
public class UI_Popup_Chest : UI_Popup
{
    /// <summary>지금 열려 있는 보관함 팝업. 없으면 null. 인벤토리 슬롯 클릭이 이걸 보고 보관함으로 넣는다.</summary>
    public static UI_Popup_Chest Current { get; private set; }

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<StorageSlotUI> slotUIs = new();

    private StorageStation storage;
    private PlayerInventory inventory;

    protected virtual void Awake()
    {
        closeButton?.onClick.AddListener(Close);
    }

    public void Bind(StorageStation station, PlayerInventory playerInventory)
    {
        Unsubscribe();

        storage = station;
        inventory = playerInventory;
        Current = this;

        if (titleText != null && storage != null)
            titleText.text = storage.DisplayName;

        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i]?.Bind(this, i);

        if (storage != null)
            storage.OnStorageChanged += RefreshUI;

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (storage == null) return;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;

            if (i < storage.SlotCount)
                slotUIs[i].Refresh(storage.Slots[i], storage.StackCounts[i]);
            else
                slotUIs[i].Refresh(null, 0);
        }
    }

    /// <summary>보관함 칸 클릭 → 스택 전부 인벤토리로 (들어갈 만큼만).</summary>
    public void OnStorageSlotClicked(int index)
    {
        if (storage == null || inventory == null) return;
        if (index < 0 || index >= storage.SlotCount) return;

        int amount = storage.StackCounts[index];
        if (amount <= 0) return;

        StructureSync.Withdraw(storage, inventory, index, amount);
        RefreshUI();
    }

    /// <summary>인벤토리 칸 클릭 → 그 스택을 보관함으로. InventorySlotUI가 호출한다.</summary>
    public void DepositFromInventory(ItemDataSO itemData, int amount)
    {
        if (storage == null || inventory == null || itemData == null || amount <= 0) return;

        StructureSync.Deposit(storage, inventory, itemData, amount);
        RefreshUI();
    }

    public override void Close()
    {
        Unsubscribe();
        if (Current == this) Current = null;
        base.Close();
    }

    private void Unsubscribe()
    {
        if (storage != null)
            storage.OnStorageChanged -= RefreshUI;
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (Current == this) Current = null;
    }
}

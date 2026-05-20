using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UI_Panel_PlayerInventory : UI_Panel
{
    #region Field
    [Header("UI 연결")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Transform equipSlotContainer;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private List<InventorySlotUI> slotUIs = new();
    [SerializeField] private InventorySlotUI headSlotUI;
    [SerializeField] private InventorySlotUI chestSlotUI;
    [SerializeField] private InventorySlotUI handSlotUI;
    private Player player;
    private bool isSubscribed;
    #endregion

    #region Property
    public PlayerInventory PlayerInventory => playerInventory;
    public Player Player
    {
        get => player;
        set
        {
            player = value;
            Bind(player != null ? player.Inventory : null);
        }
    }
    #endregion



    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        ResolveReferences();
        CollectSlots(false);
        return true;
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (playerInventory == null)
        {
            ClearSlots();
            return;
        }

        int count = Mathf.Min(slotUIs.Count, playerInventory.Slots.Count, playerInventory.StackCounts.Count);
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;

            if (i < count)
            {
                slotUIs[i].BindInventorySlot(playerInventory, i);
                slotUIs[i].Refresh(i, playerInventory.Slots[i], playerInventory.StackCounts[i]);
            }
            else
            {
                slotUIs[i].BindInventorySlot(playerInventory, i);
                slotUIs[i].Refresh(i, null, 0);
            }
        }

        RefreshEquipSlot(headSlotUI, EquipSlot.Head);
        RefreshEquipSlot(chestSlotUI, EquipSlot.Chest);
        RefreshEquipSlot(handSlotUI, EquipSlot.Hand);
        RefreshFocus(playerInventory.SelectedSlotIndex);
        RebuildLayout();
    }

    public void Bind(PlayerInventory inventory)
    {
        ResolveReferences();
        CollectSlots(false);

        if (playerInventory == inventory && isSubscribed)
        {
            ApplyQuickSlotLimit();
            RefreshUI();
            return;
        }

        Unsubscribe();
        playerInventory = inventory;

        if (playerInventory == null) return;

        playerInventory.OnInventoryChanged += RefreshUI;
        playerInventory.OnSelectedSlotChanged += RefreshFocus;
        isSubscribed = true;

        ApplyQuickSlotLimit();
        RefreshUI();
    }

    private void ResolveReferences()
    {
        if (slotContainer != null && equipSlotContainer != null) return;

        Transform inventorySlotContainer = null;
        Transform fallbackSlotContainer = null;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "InvenSlots")
                inventorySlotContainer = children[i];
            else if (children[i].name == "Slots")
                fallbackSlotContainer ??= children[i];

            if (equipSlotContainer == null && children[i].name == "EquipSlots")
                equipSlotContainer = children[i];
        }

        slotContainer ??= inventorySlotContainer != null ? inventorySlotContainer : fallbackSlotContainer;
    }

    private void CollectSlots(bool forceRecollect)
    {
        slotUIs ??= new List<InventorySlotUI>();

        if (forceRecollect || !HasValidInventorySlots())
            CollectInventorySlotUIs();

        slotUIs.Sort(CompareSlotOrder);

        if (forceRecollect || !HasValidEquipSlots())
            CollectEquipSlotUIs();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshUI;
            playerInventory.OnSelectedSlotChanged -= RefreshFocus;
        }

        isSubscribed = false;
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;
            slotUIs[i].Refresh(i, null, 0);
        }

        if (headSlotUI != null) headSlotUI.RefreshEquipment(EquipSlot.Head, null);
        if (chestSlotUI != null) chestSlotUI.RefreshEquipment(EquipSlot.Chest, null);
        if (handSlotUI != null) handSlotUI.RefreshEquipment(EquipSlot.Hand, null);
        RebuildLayout();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void RefreshEquipSlot(InventorySlotUI slotUI, EquipSlot equipSlot)
    {
        if (slotUI == null || playerInventory == null) return;

        slotUI.BindEquipmentSlot(playerInventory, equipSlot);
        slotUI.RefreshEquipment(equipSlot, playerInventory.GetEquippedItem(equipSlot));
    }

    private void RefreshFocus(int selectedIndex)
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;
            slotUIs[i].SetFocused(i == selectedIndex);
        }
    }

    private void ApplyQuickSlotLimit()
    {
        if (playerInventory == null || slotUIs.Count <= 0) return;
        playerInventory.SetQuickSlotCount(slotUIs.Count);
    }

    private void RebuildLayout()
    {
        if (!isActiveAndEnabled) return;

        Canvas.ForceUpdateCanvases();
        ForceRebuild(slotContainer);
        ForceRebuild(equipSlotContainer);
        ForceRebuild(transform);
        Canvas.ForceUpdateCanvases();
    }

    private static void ForceRebuild(Transform target)
    {
        if (target is RectTransform rectTransform)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    private void CollectInventorySlotUIs()
    {
        slotUIs ??= new List<InventorySlotUI>();
        slotUIs.Clear();

        if (slotContainer == null) return;

        foreach (Transform child in slotContainer)
        {
            if (TryCollectInventorySlot(child))
                continue;

            foreach (Transform grandChild in child)
                TryCollectInventorySlot(grandChild);
        }
    }

    private void CollectEquipSlotUIs()
    {
        headSlotUI = null;
        chestSlotUI = null;
        handSlotUI = null;

        if (equipSlotContainer == null) return;

        for (int i = 0; i < equipSlotContainer.childCount; i++)
        {
            Transform child = equipSlotContainer.GetChild(i);
            InventorySlotUI slotUI = GetOrAddSlotUI(child);
            EquipSlot slot = ResolveEquipSlot(child, i);

            switch (slot)
            {
                case EquipSlot.Head:
                    headSlotUI = slotUI;
                    break;
                case EquipSlot.Chest:
                    chestSlotUI = slotUI;
                    break;
                case EquipSlot.Hand:
                    handSlotUI = slotUI;
                    break;
            }
        }
    }

    private bool HasValidInventorySlots()
    {
        if (slotUIs == null) return false;
        if (slotUIs.Count == 0) return false;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null)
                return false;

            if (!IsInventorySlotRoot(slotUIs[i]))
                return false;
        }

        return true;
    }

    private bool HasValidEquipSlots()
    {
        return headSlotUI != null
            && chestSlotUI != null
            && handSlotUI != null;
    }

    private static int CompareSlotOrder(InventorySlotUI left, InventorySlotUI right)
    {
        int leftOrder = GetSlotOrder(left);
        int rightOrder = GetSlotOrder(right);
        int orderCompare = leftOrder.CompareTo(rightOrder);
        if (orderCompare != 0) return orderCompare;

        int leftSibling = left != null ? left.transform.GetSiblingIndex() : int.MaxValue;
        int rightSibling = right != null ? right.transform.GetSiblingIndex() : int.MaxValue;
        return leftSibling.CompareTo(rightSibling);
    }

    private bool TryCollectInventorySlot(Transform slot)
    {
        if (!IsInventorySlotRoot(slot)) return false;

        InventorySlotUI slotUI = GetOrAddSlotUI(slot);
        if (!slotUIs.Contains(slotUI))
            slotUIs.Add(slotUI);

        return true;
    }

    private bool IsInventorySlotRoot(InventorySlotUI slotUI)
    {
        return slotUI != null && IsInventorySlotRoot(slotUI.transform);
    }

    private bool IsInventorySlotRoot(Transform slot)
    {
        if (slot == null || slotContainer == null) return false;
        if (slot == slotContainer || !slot.IsChildOf(slotContainer)) return false;

        if (TryGetSlotOrder(slot.name, out _))
            return true;

        return slot.parent == slotContainer && !HasOrderedChildren(slot);
    }

    private static bool HasOrderedChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (TryGetSlotOrder(child.name, out _))
                return true;
        }

        return false;
    }

    private static int GetSlotOrder(InventorySlotUI slotUI)
    {
        if (slotUI == null) return int.MaxValue;

        if (TryGetSlotOrder(slotUI.name, out int order))
            return order;

        return slotUI.transform.GetSiblingIndex();
    }

    private static bool TryGetSlotOrder(string slotName, out int order)
    {
        order = 0;
        if (string.IsNullOrEmpty(slotName)) return false;

        for (int i = slotName.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(slotName[i])) continue;

            if (i == slotName.Length - 1) return false;
            string suffix = slotName.Substring(i + 1);
            return int.TryParse(suffix, out order);
        }

        return int.TryParse(slotName, out order);
    }

    private static InventorySlotUI GetOrAddSlotUI(Transform slot)
    {
        InventorySlotUI slotUI = slot.GetComponent<InventorySlotUI>();
        if (slotUI == null)
            slotUI = slot.gameObject.AddComponent<InventorySlotUI>();

        return slotUI;
    }

    private static EquipSlot ResolveEquipSlot(Transform slot, int index)
    {
        string name = slot.name.ToLowerInvariant();
        if (name.Contains("head")) return EquipSlot.Head;
        if (name.Contains("chest")) return EquipSlot.Chest;
        if (name.Contains("hand")) return EquipSlot.Hand;

        return index switch
        {
            0 => EquipSlot.Hand,
            1 => EquipSlot.Chest,
            2 => EquipSlot.Head,
            _ => EquipSlot.None
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ResolveReferencesForEditor();
        CollectSlots(true);
    }

    private void ResolveReferencesForEditor()
    {
        if (slotContainer != null && equipSlotContainer != null) return;

        Transform inventorySlotContainer = null;
        Transform fallbackSlotContainer = null;
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "InvenSlots")
                inventorySlotContainer = children[i];
            else if (children[i].name == "Slots")
                fallbackSlotContainer ??= children[i];

            if (equipSlotContainer == null && children[i].name == "EquipSlots")
                equipSlotContainer = children[i];
        }

        slotContainer ??= inventorySlotContainer != null ? inventorySlotContainer : fallbackSlotContainer;
    }
#endif
}

using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class InventoryDisplayUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Transform equipSlotContainer;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private bool toggleWithTab = true;
    [SerializeField] private bool hideOnStart = false;

    private readonly List<InventorySlotUI> slotUIs = new();
    private InventorySlotUI headSlotUI;
    private InventorySlotUI chestSlotUI;
    private InventorySlotUI handSlotUI;
    private CanvasGroup canvasGroup;
    private bool isSubscribed;
    private bool isVisible = true;

    public static async UniTask<InventoryDisplayUI> ShowFor(
        PlayerInventory inventory,
        string key = "InventoryUI",
        CancellationToken ct = default)
    {
        InventoryDisplayUI display = FindFirstObjectByType<InventoryDisplayUI>(FindObjectsInactive.Include);
        if (display == null && Main.Instance != null && Main.UI != null)
            display = await Main.UI.ShowHudOverlay<InventoryDisplayUI>(key, ct);

        if (display != null)
            display.Bind(inventory);

        return display;
    }

    private void Awake()
    {
        ResolveReferences();
        CollectSlots();
        SetVisible(!hideOnStart);
    }

    private void OnEnable()
    {
        Bind(playerInventory != null ? playerInventory : FindLocalPlayerInventory());
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (playerInventory == null)
            Bind(FindLocalPlayerInventory());

        if (playerInventory == null && toggleWithTab && Input.GetKeyDown(KeyCode.Tab))
            Toggle();
    }

    public void Toggle()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (canvasGroup == null)
            canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void RefreshUI()
    {
        if (playerInventory == null)
            Bind(FindLocalPlayerInventory());

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
    }

    public void Bind(PlayerInventory inventory)
    {
        ResolveReferences();
        if (slotUIs.Count == 0)
            CollectSlots();

        if (playerInventory == inventory && isSubscribed)
        {
            ApplyQuickSlotLimit();
            return;
        }

        Unsubscribe();
        playerInventory = inventory;

        if (playerInventory == null) return;

        playerInventory.OnInventoryChanged += RefreshUI;
        playerInventory.OnInventoryOpenChanged += SetVisible;
        playerInventory.OnSelectedSlotChanged += RefreshFocus;
        isSubscribed = true;

        ApplyQuickSlotLimit();
        SetVisible(playerInventory.IsOpen || !hideOnStart);
        RefreshUI();
    }

    private void ResolveReferences()
    {
        canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();

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

        slotContainer = inventorySlotContainer != null ? inventorySlotContainer : fallbackSlotContainer;
    }

    private void CollectSlots()
    {
        slotUIs.Clear();
        headSlotUI = null;
        chestSlotUI = null;
        handSlotUI = null;

        if (slotContainer != null)
        {
            CollectInventorySlotUIs();

            slotUIs.Sort(CompareSlotOrder);
        }

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

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshUI;
            playerInventory.OnInventoryOpenChanged -= SetVisible;
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

    private void CollectInventorySlotUIs()
    {
        Transform[] children = slotContainer.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == slotContainer) continue;
            if (!TryGetSlotOrder(children[i].name, out _)) continue;

            InventorySlotUI slotUI = GetOrAddSlotUI(children[i]);
            slotUIs.Add(slotUI);
        }

        if (slotUIs.Count > 0) return;

        foreach (Transform child in slotContainer)
        {
            InventorySlotUI slotUI = GetOrAddSlotUI(child);
            slotUIs.Add(slotUI);
        }
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

    private static PlayerInventory FindLocalPlayerInventory()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || !players[i].IsLocalPlayer) continue;
            if (players[i].Inventory != null)
                return players[i].Inventory;
        }

        return FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
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
}

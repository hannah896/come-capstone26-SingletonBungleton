using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class InventoryDisplayUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private bool toggleWithTab = true;
    [SerializeField] private bool hideOnStart = false;

    private readonly List<InventorySlotUI> slotUIs = new();
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
        Bind(playerInventory != null ? playerInventory : FindFirstObjectByType<PlayerInventory>());
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (playerInventory == null)
            Bind(FindFirstObjectByType<PlayerInventory>());

        if (playerInventory != null && toggleWithTab && Input.GetKeyDown(KeyCode.Tab))
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
            Bind(FindFirstObjectByType<PlayerInventory>());

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
                slotUIs[i].Refresh(i, playerInventory.Slots[i], playerInventory.StackCounts[i]);
            else
                slotUIs[i].Refresh(i, null, 0);
        }
    }

    public void Bind(PlayerInventory inventory)
    {
        if (playerInventory == inventory && isSubscribed) return;

        Unsubscribe();
        playerInventory = inventory;

        if (playerInventory == null) return;

        playerInventory.OnInventoryChanged += RefreshUI;
        playerInventory.OnInventoryOpenChanged += SetVisible;
        isSubscribed = true;

        SetVisible(playerInventory.IsOpen || !hideOnStart);
        RefreshUI();
    }

    private void ResolveReferences()
    {
        canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();

        if (slotContainer != null) return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != "Slots") continue;
            slotContainer = children[i];
            return;
        }
    }

    private void CollectSlots()
    {
        slotUIs.Clear();
        if (slotContainer == null) return;

        foreach (Transform child in slotContainer)
        {
            InventorySlotUI slotUI = child.GetComponent<InventorySlotUI>();
            if (slotUI == null)
                slotUI = child.gameObject.AddComponent<InventorySlotUI>();

            slotUIs.Add(slotUI);
        }
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshUI;
            playerInventory.OnInventoryOpenChanged -= SetVisible;
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
    }
}

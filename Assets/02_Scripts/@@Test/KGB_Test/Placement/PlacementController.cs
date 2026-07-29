using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the local player's structure-placement mode.
/// Attach this component to the Player prefab; required player references are resolved automatically.
/// </summary>
public class PlacementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera placementCamera;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private CraftingManager craftingManager;
    [SerializeField] private MonoBehaviour placementValidatorBehaviour;
    [SerializeField] private MonoBehaviour previewVisualizerBehaviour;

    [Header("Grid")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private int gridRadius = 4;
    [SerializeField] private float ghostYOffset = 0.01f;

    [Header("Layers")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private LayerMask blockingLayerMask;

    public bool IsActive { get; private set; }

    private IPlacementValidator placementValidator;
    private IPreviewVisualizer previewVisualizer;
    private Player owner;
    private PlayerInventory boundInventory;
    private CraftingManager boundCraftingManager;
    private ItemDataSO activeItemData;
    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private bool currentPlacementValid;

    private void Reset()
    {
        ConfigureDefaultLayerMasks();
    }

    private void Awake()
    {
        owner = GetComponent<Player>();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
    }

    private void OnDisable()
    {
        if (IsActive)
            CancelPlacement();

        UnbindEvents();
    }

    private void Update()
    {
        ResolveReferences();
        BindEvents();

        if (!IsLocalPlayer())
        {
            if (IsActive)
                CancelPlacement();
            return;
        }

        if (!IsActive)
            return;

        UpdatePlacement();
    }

    // 레퍼런스들을 자동으로 찾아서 할당. 필요한 경우, 플레이어의 컴포넌트나 씬에서 관련 컴포넌트를 검색.
    private void ResolveReferences()
    {
        owner ??= GetComponent<Player>();

        if (playerInventory == null)
            playerInventory = owner?.Inventory ?? GetComponent<PlayerInventory>() ?? GetComponentInChildren<PlayerInventory>(true);

        if (placementCamera == null)
            placementCamera = GetComponentInChildren<Camera>(true) ?? Camera.main;

        if (craftingManager == null)
            craftingManager = CraftingManager.Instance ?? FindFirstObjectByType<CraftingManager>();

        if (previewVisualizer == null && IsLocalPlayer())
        {
            previewVisualizer = previewVisualizerBehaviour as IPreviewVisualizer;
            if (previewVisualizer == null)
            {
                PreviewVisualizer visualizer = GetComponentInChildren<PreviewVisualizer>(true);
                if (visualizer == null)
                    visualizer = gameObject.AddComponent<PreviewVisualizer>();

                previewVisualizerBehaviour = visualizer;
                previewVisualizer = visualizer;
            }
        }

        if (placementValidator == null && IsLocalPlayer())
        {
            placementValidator = placementValidatorBehaviour as IPlacementValidator;
            if (placementValidator == null)
            {
                ConfigureDefaultLayerMasks();
                placementValidator = new PhysicsBoxValidator(blockingLayerMask);
            }
        }
    }

    // 레이어 마스크를 기본값으로 설정. "Ground" 레이어가 존재하면 groundLayerMask를 해당 레이어로 설정하고, blockingLayerMask는 groundLayerMask의 반대로 설정.
    private void ConfigureDefaultLayerMasks()
    {
        if (groundLayerMask == ~0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayerMask = 1 << groundLayer;
        }

        if (blockingLayerMask == 0)
            blockingLayerMask = ~groundLayerMask;
    }

    // 플레이어 인벤토리와 제작 매니저의 이벤트를 바인딩. 로컬 플레이어가 아닌 경우에는 이벤트를 바인딩하지 않음.
    private void BindEvents()
    {
        if (!IsLocalPlayer())
            return;

        if (playerInventory != null && boundInventory != playerInventory)
        {
            if (boundInventory != null)
                boundInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;

            playerInventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
            boundInventory = playerInventory;
            TryBeginPlacementFromSelectedSlot();
        }

        if (craftingManager != null && boundCraftingManager != craftingManager)
        {
            if (boundCraftingManager != null)
                boundCraftingManager.OnCrafted -= HandleCrafted;

            craftingManager.OnCrafted += HandleCrafted;
            boundCraftingManager = craftingManager;
        }
    }

    private void UnbindEvents()
    {
        if (boundInventory != null)
            boundInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        if (boundCraftingManager != null)
            boundCraftingManager.OnCrafted -= HandleCrafted;

        boundInventory = null;
        boundCraftingManager = null;
    }

    private bool IsLocalPlayer()
    {
        return owner == null || owner.IsLocalPlayer;
    }

    private void HandleSelectedSlotChanged(int slotIndex)
    {
        if (IsActive || playerInventory == null)
            return;

        TryBeginPlacementFromSlot(slotIndex);
    }

    private void TryBeginPlacementFromSelectedSlot()
    {
        if (playerInventory == null)
            return;

        TryBeginPlacementFromSlot(playerInventory.SelectedSlotIndex);
    }

    private void TryBeginPlacementFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerInventory.Slots.Count)
            return;

        ItemDataSO itemData = playerInventory.Slots[slotIndex];
        if (itemData == null || playerInventory.StackCounts[slotIndex] <= 0 || !IsPlaceableItem(itemData))
            return;

        BeginPlacement(itemData);
    }

    private void HandleCrafted(RecipeDataSO recipe, ItemDataSO itemData, int amount)
    {
        if (itemData == null || amount <= 0 || !IsPlaceableItem(itemData))
            return;

        BeginPlacement(itemData);
    }

    private static bool IsPlaceableItem(ItemDataSO itemData)
    {
        return itemData != null && itemData.isPlaceable && itemData.placementPrefab != null;
    }

    private void BeginPlacement(ItemDataSO itemData)
    {
        if (!IsLocalPlayer() || !IsPlaceableItem(itemData) || playerInventory == null || !playerInventory.HasItem(itemData))
            return;

        activeItemData = itemData;
        IsActive = true;
        previewVisualizer?.Show(itemData);
    }

    public void CancelPlacement()
    {
        if (!IsActive)
            return;

        IsActive = false;
        previewVisualizer?.Hide();
        ClearActiveData();
    }

    private void UpdatePlacement()
    {
        if (activeItemData == null)
        {
            CancelPlacement();
            return;
        }

        if (!TryGetMouseWorldPosition(out Vector3 worldPosition))
        {
            previewVisualizer?.SetVisible(false);
            return;
        }

        previewVisualizer?.SetVisible(true);

        Vector3 snappedPosition = GetSnappedPosition(worldPosition);
        currentPosition = snappedPosition + activeItemData.placementPivotOffset;

        HandleRotationInput();

        currentPlacementValid = placementValidator == null ||
            placementValidator.IsPlacementValid(activeItemData, currentPosition, currentRotation, gridSize);

        previewVisualizer?.UpdateView(
            currentPosition,
            currentRotation,
            snappedPosition,
            gridSize,
            gridRadius,
            ghostYOffset,
            currentPlacementValid,
            placementValidator);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && currentPlacementValid)
            ConfirmPlacement();

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            CancelPlacement();
    }

    private void ConfirmPlacement()
    {
        if (!IsPlaceableItem(activeItemData) || playerInventory == null)
        {
            CancelPlacement();
            return;
        }

        if (!playerInventory.RemoveItem(activeItemData, 1))
        {
            CancelPlacement();
            return;
        }

        Instantiate(activeItemData.placementPrefab, currentPosition, currentRotation);

        if (!playerInventory.HasItem(activeItemData))
            CancelPlacement();
    }

    private void HandleRotationInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
            currentRotation *= Quaternion.Euler(0f, -90f, 0f);

        if (Keyboard.current.eKey.wasPressedThisFrame)
            currentRotation *= Quaternion.Euler(0f, 90f, 0f);
    }

    private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (placementCamera == null || Mouse.current == null)
            return false;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Ray ray = placementCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayerMask))
        {
            worldPosition = hit.point;
            return true;
        }

        return false;
    }

    private Vector3 GetSnappedPosition(Vector3 worldPosition)
    {
        if (activeItemData == null || !activeItemData.placementSnapToGrid)
            return worldPosition;

        float x = Mathf.Round(worldPosition.x / gridSize) * gridSize;
        float z = Mathf.Round(worldPosition.z / gridSize) * gridSize;

        return new Vector3(x, worldPosition.y, z);
    }

    private void ClearActiveData()
    {
        activeItemData = null;
        currentRotation = Quaternion.identity;
    }
}

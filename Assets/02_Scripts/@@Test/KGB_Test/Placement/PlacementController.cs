using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력과 시각화를 관리하여 플레이어가 아이템을 배치할 수 있도록 하는 컨트롤러입니다.
///// </summary>
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

    public bool IsActive { get; private set; }

    private IPlacementValidator placementValidator;
    private IPreviewVisualizer previewVisualizer;

    private ItemDataSO activeItemData;

    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private bool currentPlacementValid;

    private void Awake()
    {
        if (placementCamera == null)
            placementCamera = Camera.main;

        placementValidator = placementValidatorBehaviour as IPlacementValidator;
        previewVisualizer = previewVisualizerBehaviour as IPreviewVisualizer;
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void Update()
    {
        if (!IsActive)
            return;

        UpdatePlacement();
    }

    private void BindEvents()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (craftingManager == null)
            craftingManager = CraftingManager.Instance ?? FindFirstObjectByType<CraftingManager>();

        if (playerInventory != null)
            playerInventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        if (craftingManager != null)
            craftingManager.OnCrafted += HandleCrafted; //TODO: CraftingManager에 OnCrafted 이벤트 추가 필요
        //                                                //      OnClickedPlaceButton 이벤트로 변경하여 UI에서 배치 모드 진입하도록 변경하는 것도 
    }

    private void UnbindEvents()
    {
        if (playerInventory != null)
            playerInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        if (craftingManager != null)
            craftingManager.OnCrafted -= HandleCrafted;
    }
    // 슬롯 변경 시 해당 슬롯의 아이템이 배치 가능한지 체크하여 배치 모드로 진입
    private void HandleSelectedSlotChanged(int slotIndex)
    {
        if (IsActive || playerInventory == null)
            return;

        if (slotIndex < 0 || slotIndex >= playerInventory.Slots.Count)
            return;

        ItemDataSO itemData = playerInventory.Slots[slotIndex];
        if (itemData == null || playerInventory.StackCounts[slotIndex] <= 0)
            return;

        if (!IsPlaceableItem(itemData))
            return;

        BeginPlacement(itemData);
    }
    // 제작 완료 시 결과 아이템이 배치 가능한지 체크하여 배치 모드로 진입

    private void HandleCrafted(RecipeDataSO recipe, ItemDataSO itemData, int amount)
    {
        if (itemData == null || amount <= 0)
            return;

        if (!IsPlaceableItem(itemData))
            return;

        BeginPlacement(itemData);
    }
    private bool IsPlaceableItem(ItemDataSO itemData)
    {
        return itemData.isPlaceable && itemData.placementPrefab != null;
    }


    private void BeginPlacement(ItemDataSO itemData)
    {
        if (itemData == null)
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
        if (!TryGetMouseWorldPosition(out Vector3 worldPosition))
        {
            previewVisualizer?.SetVisible(false);
            return;
        }

        previewVisualizer?.SetVisible(true);

        Vector3 snappedPosition = GetSnappedPosition(worldPosition);
        currentPosition = snappedPosition + activeItemData.placementPivotOffset;    //TODO: ItemDataSO에 placementPivotOffset 추가 필요

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
        if (activeItemData == null || activeItemData.placementPrefab == null)         
            return;

        Instantiate(activeItemData.placementPrefab, currentPosition, currentRotation);

        if (playerInventory != null && activeItemData != null)
            playerInventory.RemoveItem(activeItemData, 1);
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
        if (activeItemData == null || !activeItemData.placementSnapToGrid)                  //TODO: ItemDataSO에 placementSnapToGrid 추가 필요
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

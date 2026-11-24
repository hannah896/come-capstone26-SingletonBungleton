using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력 콜백을 받아 PlayerInputData에 기록하는 핸들러
/// InputActions_CameraMove 패턴을 참고하여 구조화
/// </summary>
public sealed class InputActions_PlayerInputHandler : InputActions
{
    private PlayerInputData inputData;
    private Player player;
    private bool isConnected;
    private int lastEquipInputFrame = -1;

    public InputActions_PlayerInputHandler(InputManager manager) : base(manager) { }

    #region Setup

    /// <summary>
    /// Player와 InputData를 바인딩한다.
    /// </summary>
    public void Bind(Player playerInstance, PlayerInputData data)
    {
        player = playerInstance;
        inputData = data;
    }

    #endregion

    #region Input Connect/Disconnect

    public override void Connect()
    {
        if (inputData == null)
        {
            Debug.LogWarning("[InputActions_PlayerInputHandler] InputData not bound!");
            return;
        }

        if (Manager?.Actions?.Player == null)
        {
            Debug.LogError("[InputActions_PlayerInputHandler] Manager.Actions.Player is null!");
            return;
        }

        var p = Manager.Actions.Player;
        if (isConnected)
            Disconnect();
        else
        {
            p.Interact.started -= OnEquip;
            p.Interact.performed -= OnEquip;
        }

        p.Move.performed += OnMovePerformed;
        p.Move.canceled += OnMoveCanceled;
        p.Look.performed += OnLookPerformed;
        p.Look.canceled += OnLookCanceled;
        p.Sprint.performed += OnSprintPerformed;
        p.Sprint.canceled += OnSprintCanceled;
        p.Jump.performed += OnJumpPerformed;
        p.Attack.performed += OnAttackPerformed;
        p.Crouch.performed += OnCrouchPerformed;
        p.Interact.started += OnEquip;
        p.PickUp.performed += OnPickup;
        p.Drop.performed += OnDrop;
        p.ToolUse.performed += OnToolUsePerformed;
        p.ToolUse.started += OnToolUseStarted;
        p.ToolUse.canceled += OnToolUseCanceled;
        p.RotateView.started += OnRotateViewStarted;
        p.RotateView.canceled += OnRotateViewCanceled;
        p.ScrollWheel.performed += OnScrollWheel;
        p.ScrollWheel.canceled += OnScrollCanceled;
        p.InventorySlot.performed += OnInventorySlot;
        isConnected = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[InputActions_PlayerInputHandler] Connected");
#endif
    }

    public override void Disconnect()
    {
        if (Manager?.Actions?.Player == null) return;

        var p = Manager.Actions.Player;
        if (!isConnected)
        {
            p.Interact.performed -= OnEquip;
            return;
        }

        p.Move.performed -= OnMovePerformed;
        p.Move.canceled -= OnMoveCanceled;
        p.Look.performed -= OnLookPerformed;
        p.Look.canceled -= OnLookCanceled;
        p.Sprint.performed -= OnSprintPerformed;
        p.Sprint.canceled -= OnSprintCanceled;
        p.Jump.performed -= OnJumpPerformed;
        p.Attack.performed -= OnAttackPerformed;
        p.Crouch.performed -= OnCrouchPerformed;
        p.Interact.started -= OnEquip;
        p.Interact.performed -= OnEquip;
        p.PickUp.performed -= OnPickup;
        p.Drop.performed -= OnDrop;
        p.ToolUse.performed -= OnToolUsePerformed;
        p.ToolUse.started -= OnToolUseStarted;
        p.ToolUse.canceled -= OnToolUseCanceled;
        p.RotateView.started -= OnRotateViewStarted;
        p.RotateView.canceled -= OnRotateViewCanceled;
        p.ScrollWheel.performed -= OnScrollWheel;
        p.ScrollWheel.canceled -= OnScrollCanceled;
        p.InventorySlot.performed -= OnInventorySlot;

        // 홀드 상태가 true로 남으면 다음 액션이 저절로 반복되거나 시야 회전이 고정된다.
        if (inputData != null)
        {
            inputData.ToolUseHeld = false;
            inputData.RotateViewHeld = false;
            inputData.LookInput = Vector2.zero;
        }

        isConnected = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[InputActions_PlayerInputHandler] Disconnected");
#endif
    }

    #endregion

    #region 연속 입력 콜백

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.MoveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.MoveInput = Vector2.zero;
    }

    private void OnLookPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.LookInput = ctx.ReadValue<Vector2>();
    }

    private void OnLookCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.LookInput = Vector2.zero;
    }

    private void OnSprintPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.SprintHeld = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.SprintHeld = false;
    }

    #endregion

    #region 이벤트 입력 콜백 (한 프레임만 유효)

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.JumpPressed = true;
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.AttackPressed = true;
    }

    private void OnCrouchPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.CrouchPressed = true;
    }

    #endregion

    // 스크롤
    private void OnScrollWheel(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        if (ShouldBlockQuickSlotScroll()) return;

        float y = ctx.ReadValue<Vector2>().y;
        if (Mathf.Abs(y) > 0.01f)
            inputData.QuickSlotScrollDelta = y > 0f ? -1 : 1;
    }

    private bool ShouldBlockQuickSlotScroll()
    {
        // 우클릭 시점 회전 중 커서가 화면 중앙에 잠겨 있을 때는 퀵슬롯 스크롤을 허용한다.
        if (Cursor.lockState == CursorLockMode.Locked) return false;
        if (Manager == null || Mouse.current == null) return false;
        if (!Manager.TryGetTopUIHit(Mouse.current.position.ReadValue(), out RaycastResult topHit))
            return false;

        GameObject hitObject = topHit.gameObject;
        if (hitObject == null) return false;

        // 크래프팅/작업대/요리솥/미니맵 등 스크롤 가능한 UI 위에서는 UI 스크롤만 동작하도록 차단한다.
        if (ExecuteEvents.GetEventHandler<IScrollHandler>(hitObject) != null)
            return true;

        // 플레이어 인벤토리 바 위에서만 퀵슬롯 스크롤을 허용하고 다른 UI 패널 위에서는 차단한다.
        return hitObject.GetComponentInParent<UI_Panel_PlayerInventory>() == null;
    }

    // 슬롯 직접 선택
    private void OnInventorySlot(InputAction.CallbackContext ctx)
    {
        string controlName = ctx.control.name;
        if (int.TryParse(controlName, out int num))
            inputData.QuickSlotIndex = num == 0 ? 9 : num - 1;
    }

    private void OnEquip(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        if (!ctx.started) return;

        int frame = Time.frameCount;
        if (lastEquipInputFrame == frame) return;

        lastEquipInputFrame = frame;
        inputData.EquipSelectedPressed = true;
    }

    private void OnPickup(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.PickupPressed = true;
    }

    private void OnDrop(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.DropPressed = true;
    }

    private void OnToolUsePerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.ToolUsePressed = true;
    }

    // 누르고 있는 동안 벌목/채굴을 반복하기 위해 홀드 상태를 따로 기록한다.
    private void OnToolUseStarted(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.ToolUseHeld = true;
    }

    private void OnToolUseCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.ToolUseHeld = false;
    }

    // 우클릭(RotateView)을 누르는 동안 시야 회전 활성화
    private void OnRotateViewStarted(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;

        // UI 위에서 우클릭한 경우(아이템 우클릭 사용, UI 조작 등) 시야 회전을 시작하지 않는다.
        if (Cursor.lockState != CursorLockMode.Locked
            && Manager != null
            && Mouse.current != null
            && Manager.IsPointerOverUI(Mouse.current.position.ReadValue()))
            return;

        inputData.RotateViewHeld = true;
    }

    private void OnRotateViewCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.RotateViewHeld = false;
    }

    private void OnScrollCanceled(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.QuickSlotScrollDelta = 0;
    }

}

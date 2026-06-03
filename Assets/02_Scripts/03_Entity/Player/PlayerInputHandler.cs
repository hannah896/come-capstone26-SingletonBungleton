using UnityEngine;
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
        p.ToolUse.performed += OnToolUsePerformed;
        p.RotateView.started += OnRotateViewStarted;
        p.RotateView.canceled += OnRotateViewCanceled;
        p.ScrollWheel.performed += OnScrollWheel;
        p.ScrollWheel.canceled += OnScrollCanceled;
        p.Previous.performed += OnPrevious;
        p.Next.performed += OnNext;
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
        p.ToolUse.performed -= OnToolUsePerformed;
        p.RotateView.started -= OnRotateViewStarted;
        p.RotateView.canceled -= OnRotateViewCanceled;
        p.ScrollWheel.performed -= OnScrollWheel;
        p.ScrollWheel.canceled -= OnScrollCanceled;
        p.Previous.performed -= OnPrevious;
        p.Next.performed -= OnNext;
        p.InventorySlot.performed -= OnInventorySlot;
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
        float y = ctx.ReadValue<Vector2>().y;
        if (Mathf.Abs(y) > 0.01f)
            inputData.QuickSlotScrollDelta = y > 0f ? -1 : 1;
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

    private void OnToolUsePerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.ToolUsePressed = true;
    }

    // Ctrl 키를 누르는 동안 시야 회전 활성화
    private void OnRotateViewStarted(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
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

    private void OnPrevious(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.QuickSlotScrollDelta = 1;
    }

    private void OnNext(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.QuickSlotScrollDelta = -1;
    }

}

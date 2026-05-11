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

        var playerActions = Manager.Actions.Player;

        // 연속 입력
        playerActions.Move.performed += OnMovePerformed;
        playerActions.Move.canceled += OnMoveCanceled;
        
        playerActions.Look.performed += OnLookPerformed;
        playerActions.Look.canceled += OnLookCanceled;
        
        playerActions.Sprint.performed += OnSprintPerformed;
        playerActions.Sprint.canceled += OnSprintCanceled;

        // 이벤트 입력 (한 프레임만 유효)
        playerActions.Jump.performed += OnJumpPerformed;
        playerActions.Attack.performed += OnAttackPerformed;
        playerActions.Crouch.performed += OnCrouchPerformed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[InputActions_PlayerInputHandler] Connected");
#endif
    }

    public override void Disconnect()
    {
        if (inputData == null) return;

        if (Manager?.Actions?.Player == null) return;

        var playerActions = Manager.Actions.Player;

        playerActions.Move.performed -= OnMovePerformed;
        playerActions.Move.canceled -= OnMoveCanceled;
        
        playerActions.Look.performed -= OnLookPerformed;
        playerActions.Look.canceled -= OnLookCanceled;
        
        playerActions.Sprint.performed -= OnSprintPerformed;
        playerActions.Sprint.canceled -= OnSprintCanceled;

        playerActions.Jump.performed -= OnJumpPerformed;
        playerActions.Attack.performed -= OnAttackPerformed;
        playerActions.Crouch.performed -= OnCrouchPerformed;

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
}

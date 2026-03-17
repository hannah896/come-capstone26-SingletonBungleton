using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 입력 콜백을 받아 PlayerInputData에 기록하는 핸들러
/// </summary>
public class InputActions_PlayerInputHandler : InputActions
{
    private PlayerInputData inputData;
    private Player _player;

    public InputActions_PlayerInputHandler(InputManager manager) : base(manager) { }

    /// <summary>
    /// Player가 소유하는 InputData를 바인딩한다.
    /// </summary>
    public void Bind(PlayerInputData data)
    {
        inputData = data;
    }

    public override void Connect()
    {
        var player = Manager.Actions.Player;

        // 연속 입력
        player.Move.performed += OnMovePerformed;
        player.Move.canceled += OnMoveCanceled;
        player.Look.performed += OnLookPerformed;
        player.Look.canceled += OnLookCanceled;
        player.Sprint.performed += OnSprintPerformed;
        player.Sprint.canceled += OnSprintCanceled;

        // 이벤트 입력
        player.Jump.performed += OnJumpPerformed;
        player.Attack.performed += OnAttackPerformed;
        player.Crouch.performed += OnCrouchPerformed;
        player.Interact.performed += OnInteractPerformed;
    }

    public override void Disconnect()
    {
        var player = Manager.Actions.Player;

        player.Move.performed -= OnMovePerformed;
        player.Move.canceled -= OnMoveCanceled;
        player.Look.performed -= OnLookPerformed;
        player.Look.canceled -= OnLookCanceled;
        player.Sprint.performed -= OnSprintPerformed;
        player.Sprint.canceled -= OnSprintCanceled;

        player.Jump.performed -= OnJumpPerformed;
        player.Attack.performed -= OnAttackPerformed;
        player.Crouch.performed -= OnCrouchPerformed;
        player.Interact.performed -= OnInteractPerformed;
    }

    // --- 연속 입력 콜백 ---

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.MoveInput = ctx.ReadValue<Vector2>();
        var speed = _player.Stat.MoveSpeed;

        _player.Rb.MovePosition(_player.transform.position + new Vector3(inputData.MoveInput.x*, 0, inputData.MoveInput.y) * Time.deltaTime * 5f);
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

    // --- 이벤트 입력 콜백 ---

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

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (inputData == null) return;
        inputData.InteractPressed = true;
    }
}

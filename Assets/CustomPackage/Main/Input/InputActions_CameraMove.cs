using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 드래그 혹은 터치패드 드래그로 카메라를 이동시키는 인풋 액션
/// </summary>
public sealed class InputActions_CameraMove : InputActions
{
    public InputActions_CameraMove(InputManager manager) : base(manager){}
        
    private Vector2 _lastPos;
    private Vector2 _startPos;
    private bool _isDragging;
    private const float DragThreshold = 5f;

    public override void Connect()
    {
        Manager.Actions.Mobile.Click.started += OnStart;
        Manager.Actions.Mobile.Click.canceled += OnEnd;
        Manager.Actions.Mobile.Point.performed += OnMove;
    }
    public override void Disconnect()
    {
        Manager.Actions.Mobile.Click.started -= OnStart;
        Manager.Actions.Mobile.Click.canceled -= OnEnd;
        Manager.Actions.Mobile.Point.performed -= OnMove;
        _isDragging = false;
    }
    private void OnStart(InputAction.CallbackContext ctx)
    {
        var pos = Manager.Actions.Mobile.Point.ReadValue<Vector2>();
        if (Manager.IsPointerOverUI(pos)) return;
        _isDragging = true;
        _startPos = pos;
        _lastPos = pos;
    }
    private void OnMove(InputAction.CallbackContext ctx)
    {
        if (!_isDragging) return;
        var currentPos = ctx.ReadValue<Vector2>();
        if (Vector2.Distance(_startPos, currentPos) < DragThreshold)
        {
            _lastPos = currentPos;
            return;
        }
        Vector3 delta = Manager.ScreenToWorld(currentPos) - Manager.ScreenToWorld(_lastPos);
        if (MainCamera != null) MainCamera.transform.position -= delta;
        _lastPos = currentPos;
    }
    private void OnEnd(InputAction.CallbackContext ctx) => _isDragging = false;
}
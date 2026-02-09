using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// 마우스 스크롤 또는 터치 패드 핀치로 줌인 줌아웃 하는 인풋 액션 클래스.
/// </summary>
public sealed class InputActions_CameraZoom : InputActions
{
    public InputActions_CameraZoom(InputManager manager) : base(manager) { }
    
    private float _lastPinchDist;
    private bool _isPinching;

    public override void Connect()
    {
        Manager.Actions.Mobile.ZoomScroll.performed += OnScroll;
        Manager.Actions.Mobile.Point.performed += OnPinch;
    }
    public override void Disconnect()
    {
        Manager.Actions.Mobile.ZoomScroll.performed -= OnScroll;
        Manager.Actions.Mobile.Point.performed -= OnPinch;
        _isPinching = false;
    }
    private void OnScroll(InputAction.CallbackContext ctx)
    {
        var pos = Manager.Actions.Mobile.Point.ReadValue<Vector2>();
        if (Manager.IsPointerOverUI(pos)) return;
        float delta = ctx.ReadValue<Vector2>().y;
        if (!Mathf.Approximately(delta, 0f)) ApplyZoom(delta * 0.01f);
    }
    private void OnPinch(InputAction.CallbackContext ctx)
    {
        var touches = Touch.activeTouches;
        if (touches.Count < 2)
        {
            _isPinching = false;
            return;
        }
        for (int i = 0; i < touches.Count; i++)
        {
            if (Manager.IsPointerOverUI(touches[i].screenPosition)) return;
        }
        float currentDist = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
        if (!_isPinching)
        {
            _lastPinchDist = currentDist;
            _isPinching = true;
            return;
        }
        float delta = currentDist - _lastPinchDist;
        ApplyZoom(delta * 0.05f);
        _lastPinchDist = currentDist;
    }
    private void ApplyZoom(float amount)
    {
        var cam = MainCamera;
        if (cam == null) return;
        if (cam.orthographic)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - amount, 3f, 15f);
        else
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - amount, 20f, 80f);
    }
}
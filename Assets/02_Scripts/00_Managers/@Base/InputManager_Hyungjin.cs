using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Scripting;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

[Flags]
public enum InputActionType_HyungJin
{
    None = 0,
    CameraMove = 1 << 0,
    CameraZoom = 1 << 1,
    All = ~0
}

public class InputManager_HyungJin : CoreManager
{
    private InputSystem_Actions _input;
    private InputActionType_HyungJin _currentMask = InputActionType_HyungJin.None;
    private readonly Dictionary<InputActionType_HyungJin, InputActions_HyungJin> _actions = new();

    private PointerEventData _pointerData;
    private readonly List<RaycastResult> _raycastResults = new(16);

    public InputSystem_Actions Actions => _input;

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        EnhancedTouchSupport.Enable();
        _input = new InputSystem_Actions();
        _input.Enable();

        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(InputActions_HyungJin).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in types)
        {
            var instance = (InputActions_HyungJin)Activator.CreateInstance(type);
            instance.Init(this);
            _actions[instance.GetInputActionType()] = instance;
        }

        SetMask(InputActionType_HyungJin.All);
    }

    public void SetMask(InputActionType_HyungJin newMask)
    {
        if (_currentMask == newMask) return;
        var changed = _currentMask ^ newMask;

        foreach (var action in _actions.Values)
        {
            var type = action.GetInputActionType();
            if ((changed & type) == 0) continue;

            if ((newMask & type) != 0) action.Connect();
            else action.Disconnect();
        }
        _currentMask = newMask;
    }

    public void AddMask(InputActionType_HyungJin mask) => SetMask(_currentMask | mask);
    public void RemoveMask(InputActionType_HyungJin mask) => SetMask(_currentMask & ~mask);
    public bool IsActive(InputActionType_HyungJin mask) => (_currentMask & mask) != 0;

    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        if (_pointerData == null) _pointerData = new PointerEventData(EventSystem.current);

        _pointerData.position = screenPos;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerData, _raycastResults);
        return _raycastResults.Count > 0;
    }

    public Vector3 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        float depth = cam.orthographic ? -cam.transform.position.z : 0f;
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
    }
}

[Preserve]
public abstract class InputActions_HyungJin
{
    protected InputManager_HyungJin Manager;
    protected Camera MainCamera => Camera.main;

    public void Init(InputManager_HyungJin manager) => Manager = manager;
    public abstract InputActionType_HyungJin GetInputActionType();
    public abstract void Connect();
    public abstract void Disconnect();
}

[Preserve]
public sealed class InputActions_CameraMove_HyungJin : InputActions_HyungJin
{
    private Vector2 _lastPos;
    private Vector2 _startPos;
    private bool _isDragging;
    private const float DragThreshold = 5f;

    public override InputActionType_HyungJin GetInputActionType() => InputActionType_HyungJin.CameraMove;

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

[Preserve]
public sealed class InputActions_CameraZoom_HyungJin : InputActions_HyungJin
{
    private float _lastPinchDist;
    private bool _isPinching;

    public override InputActionType_HyungJin GetInputActionType() => InputActionType_HyungJin.CameraZoom;

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
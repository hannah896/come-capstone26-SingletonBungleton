using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager
{
    private InputSystem_Actions _input;
    public InputSystem_Actions.PlayerActions Actions => _input.Player;

    public void Init()
    {
        _input = new InputSystem_Actions();
        _input.Enable();
    }

    public Vector2 GetPointerPosition() => Actions.Point.ReadValue<Vector2>();
    public bool IsPointerPressed() => Actions.Click.IsPressed();

    public bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;

        System.Collections.Generic.List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    public void BindAction(InputAction action,
       Action<InputAction.CallbackContext> started = null,
       Action<InputAction.CallbackContext> performed = null,
       Action<InputAction.CallbackContext> canceled = null)
    {
        if(action == null) return;

        if (started != null) action.started += started;
        if (performed != null) action.performed += performed;
        if (canceled != null) action.canceled += canceled;
    }

    public void UnBindAction(InputAction action,
       Action<InputAction.CallbackContext> started = null,
       Action<InputAction.CallbackContext> performed = null,
       Action<InputAction.CallbackContext> canceled = null)
    {
        if (action == null) return;

        if (started != null) action.started -= started;
        if (performed != null) action.performed -= performed;
        if (canceled != null) action.canceled -= canceled;
    }

    public void ClearAll()
    {
        _input.Disable();
        _input.Enable();
    }
}

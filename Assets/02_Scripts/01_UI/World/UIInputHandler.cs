using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UI 입력 콜백을 받는다.
/// </summary>
public sealed class UIMapInputHandler : InputActions
{
    private UI_Hud_WorldState _worldStateHud;
    private bool _isConnected;


    public UIMapInputHandler(InputManager manager) : base(manager)
    {
    }

    public override void Connect()
    {
        if (_isConnected || Manager?.Actions?.UI == null) return;

        Manager.Actions.UI.OpenMap.performed += HandleOpenMap;
        _isConnected = true;
    }

    public override void Disconnect()
    {
        if (!_isConnected || Manager?.Actions?.UI == null) return;

        Manager.Actions.UI.OpenMap.performed -= HandleOpenMap;
        _isConnected = false;
        _worldStateHud = null;
    }

    private void HandleOpenMap(InputAction.CallbackContext _)
    {
        if (!_isConnected) return;
        if (_worldStateHud == null || !_worldStateHud.isActiveAndEnabled)
            _worldStateHud = Object.FindFirstObjectByType<UI_Hud_WorldState>();
        if (_worldStateHud != null && _worldStateHud.isActiveAndEnabled)
            _worldStateHud.ToggleMap();
    }
}

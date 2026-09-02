using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// UI 입력 콜백을 받는다.
/// </summary>
public sealed class UIMapInputHandler : InputActions
{
    private UI_Popup_WorldMap _worldMapPopup;
    private bool _isConnected;
    private bool _isToggling;


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
    }

    private void HandleOpenMap(InputAction.CallbackContext _)
    {
        ToggleMapAsync().Forget();
    }

    private async UniTaskVoid ToggleMapAsync()
    {
        if (_isToggling || !_isConnected) return;

        _isToggling = true;
        try
        {
            if (_worldMapPopup != null)
            {
                _worldMapPopup.Close();
                await UniTask.WaitUntil(() => _worldMapPopup == null);
                return;
            }

            _worldMapPopup = await Extensions.ShowPopup<UI_Popup_WorldMap>(
                clickGuard: true,
                clickClose: false);
        }
        catch (OperationCanceledException)
        {
            // 씬 전환 중 팝업 로드가 취소되는 정상 경로다.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            _isToggling = false;
        }
    }
}

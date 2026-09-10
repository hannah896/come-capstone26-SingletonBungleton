using UnityEngine.InputSystem;

/// <summary>
/// ESC로 일시정지 팝업을 여닫는다.
///
/// UI 맵의 <c>Cancel</c> 액션(<c>*/{Cancel}</c> → 키보드 Escape)을 쓴다.
/// 전용 Pause 액션을 새로 만들지 않은 이유는, 이 프로젝트에서 Cancel을 소비하는 곳이 없고
/// 입력 에셋(.inputactions)과 생성 코드(InputSystem_Actions.cs)를 함께 손봐야 하기 때문이다.
/// 나중에 Cancel을 일반 팝업 닫기에도 쓰게 되면 그때 전용 액션으로 분리하면 된다.
///
/// 열기 가능 여부(사망 중 차단)와 실제 열기/닫기는 <see cref="UI_Popup_Pause.Toggle"/>가 판단한다.
/// </summary>
public sealed class PauseInputHandler : InputActions
{
    private bool _isConnected;

    public PauseInputHandler(InputManager manager) : base(manager)
    {
    }

    public override void Connect()
    {
        if (_isConnected || Manager?.Actions?.UI == null) return;

        Manager.Actions.UI.Cancel.performed += HandleCancel;
        _isConnected = true;
    }

    public override void Disconnect()
    {
        if (!_isConnected || Manager?.Actions?.UI == null) return;

        Manager.Actions.UI.Cancel.performed -= HandleCancel;
        _isConnected = false;
    }

    private void HandleCancel(InputAction.CallbackContext _)
    {
        if (!_isConnected) return;

        UI_Popup_Pause.Toggle();
    }
}

using UnityEngine;

public class InputActions_CameraMove : InputActions
{
    private Vector2 _lastPanPosition;
    private Vector2 CameraMoveMinPos => Main.Board.Current.Min;
    private Vector2 CameraMoveMaxPos => Main.Board.Current.Max;

    public override InputActionType GetInputActionType() => InputActionType.CameraMove;

    public override void ConnectInputController()
    {
        InputController.OnActionPositionLeftStarted += PositionLeft;
        InputController.OnActionPositionLeftCanceled += LeftClickLeftCanceled;
        InputController.OnActionPositionLeftDrag += OnDrag;
    }

    public override void DisconnectInputController()
    {
        InputController.OnActionPositionLeftStarted -= PositionLeft;
        InputController.OnActionPositionLeftCanceled -= LeftClickLeftCanceled;
        InputController.OnActionPositionLeftDrag -= OnDrag;
    }
    
    private void PositionLeft(Vector2 position)
    {
        _lastPanPosition = position;
    }
    private void LeftClickLeftCanceled(Vector2 position)
    {
        _lastPanPosition = position;;
    }

    private void OnDrag(Vector2 position)
    {
        Vector2 worldDelta = position - _lastPanPosition;
        Vector3 cameraPos = Camera.transform.position;
        cameraPos -= (Vector3)worldDelta;
        if (!Main.IsEditorMode)
        {
            cameraPos.x = Mathf.Clamp(cameraPos.x, CameraMoveMinPos.x, CameraMoveMaxPos.x);
            cameraPos.y = Mathf.Clamp(cameraPos.y, CameraMoveMinPos.y, CameraMoveMaxPos.y);
        }

        if (Camera.transform.position == cameraPos) return;
        Camera.transform.position = cameraPos;
    }
}

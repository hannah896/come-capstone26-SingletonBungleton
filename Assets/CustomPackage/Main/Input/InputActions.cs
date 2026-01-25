using UnityEngine;

public abstract class InputActions
{
    protected InputController InputController;
    protected Camera Camera => InputController.Camera;
    public void Init(InputController inputController) => InputController = inputController;
    public abstract InputActionType GetInputActionType();
    public abstract void ConnectInputController();
    public abstract void DisconnectInputController();
}

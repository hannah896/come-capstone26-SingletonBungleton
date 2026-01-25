using UnityEngine;

public class InputActions_CameraZoom : InputActions
{
#if UNITY_EDITOR
    public const float ZoomSpeed = 1f;
#else
    public const float ZoomSpeed = 0.05f;
#endif
    public const float MinZoom = 3f;
    #if UNITY_STANDALONE || UNITY_STANDALONE_WIN
    public const float MaxZoom = 100f;
#else
    public const float MaxZoom = 20f;
#endif
    public override InputActionType GetInputActionType() => InputActionType.CameraZoom;

    public override void ConnectInputController()
    {
        InputController.OnActionZoomPerformed += ApplyZoom;
        InputController.OnActionScrollPerformed += ApplyZoom;
    }

    public override void DisconnectInputController()
    {
        InputController.OnActionZoomPerformed -= ApplyZoom;
        InputController.OnActionScrollPerformed -= ApplyZoom;
    }
    
    private void ApplyZoom(float delta)
    {
        if (Camera.orthographic)
        {
            Camera.orthographicSize -= delta * ZoomSpeed;
            Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize, MinZoom, MaxZoom);
        }
        else
        {
            Camera.fieldOfView -= delta * ZoomSpeed;
            Camera.fieldOfView = Mathf.Clamp(Camera.fieldOfView, 20f, 60f);
        }
    }
}

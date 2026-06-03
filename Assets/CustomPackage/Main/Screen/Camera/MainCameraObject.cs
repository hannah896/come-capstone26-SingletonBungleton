using System;
using UnityEngine;

public class MainCameraObject : InitBehaviour
{
    public MainCamera MainCamera { get; private set; }
    public event Action<Camera> OnChangeCamera;
    public Camera Camera
    {
        get
        {
            if (_camera == null)
            {
                Camera = GetComponentInChildren<Camera>();
            }
            return _camera;
        }
        private set
        {
            _camera = value;
            OnChangeCamera?.Invoke(Camera);
        }
    }

    private Camera _camera;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        Camera = GetComponentInChildren<Camera>();
        if (Camera == null)
        {
            Debug.LogError("Camera not found");
        }

        return true;
    }

    public void Set(MainCamera mainCamera)
    {
        Initialize();
        MainCamera = mainCamera;
        transform.SetParent(Main.Instance.transform);
    }
    
    public void AddCameraChangeAction(Action<Camera> action) => OnChangeCamera += action;
    public void RemoveCameraChangeAction(Action<Camera> action) => OnChangeCamera -= action;
    public void ClearCameraChangeAction() => OnChangeCamera = null;
    public void SetColorCameraBG(Color color) => Camera.backgroundColor = color;
}

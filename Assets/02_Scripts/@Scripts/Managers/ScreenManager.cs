using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 카메라의 이동, 크기 등을 관리해주는 매니저
/// </summary>
public class ScreenManager : CoreManager
{
    #region Const.
    private static readonly Color CameraColorBG = new Color(1, 1, 1, 1);
    private static float CameraYBuffer = 10f;
    #endregion

    #region Properties
    public Vector2 ReferenceResolution => new(1080f, 1920f);

    public Vector2 CameraSize { get; private set; }
    public Vector3 CameraPosition { get; private set; }
    public float Aspect { get; private set; }
    public float ReverseAspect { get; private set; }

    public MainCamera MainCamera
    {
        get
        {
            if (_mainCamera == null)
            {
                _mainCamera = new();
                _mainCamera.GenerateObject();
            }
            return _mainCamera;
        }
    }

    public Camera Camera => MainCamera.Camera;
    // private SpriteRenderer _background;
    #endregion

    #region Fields
    private MainCamera _mainCamera;
    #endregion

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        Application.targetFrameRate = 60;
        if (_mainCamera == null)
        {
            _mainCamera = new();
            _mainCamera.GenerateObject();
        }
    }

    public override void Clear() { base.Clear(); }

    public void SetCamera()
    {
        if (Main.Scene.Current is GameScene)
        {
            UI_GameScene sceneUI = (Main.Scene.Current as GameScene)?.SceneUI;
            float topUIRatio = sceneUI.TopUIRatio;
            float bottomUIRatio = sceneUI.BottomUIRatio;
            SetGameCamera(topUIRatio, bottomUIRatio);
            Main.Screen.MainCamera.SetColorCameraBG(CameraColorBG);
        }
    }

    private void SetGameCamera(float topUIRatio = 0, float bottomUIRatio = 0)
    {
        // #1. 게임 보드 크기 받아오기.
        Board board = Main.Board.Current;
        Vector2 center = board.Center;
        Vector2 size = board.Size;

        // #2. 화면 비율 받아오기.
        Aspect = Screen.width / (float)Screen.height;
        ReverseAspect = Screen.height / (float)Screen.width;

        // #3. 카메라 크기 계산.
        float xMin = (Board.MarginLeft + Board.MarginRight + size.x) * 0.5f * ReverseAspect;
        float yMin = (Board.MarginTop + Board.MarginBottom + size.y) * 0.5f / (1 - topUIRatio - bottomUIRatio);
        float cameraSizeY = Mathf.Max(xMin, yMin) + CameraYBuffer;
        float cameraSizeX = cameraSizeY * Aspect;
        CameraSize = new(cameraSizeX, cameraSizeY);

        // #5. 카메라 위치 계산.
        float cameraCenterX = center.x;
        float cameraCenterY = center.y;
        CameraPosition = new(cameraCenterX, cameraCenterY, -10);

        // #6. 카메라 설정 적용.
        Camera.orthographicSize = CameraSize.y;
        Camera.transform.position = CameraPosition;
    }

    public Vector3 GetScreenPos(Vector3 worldPos) => Camera.WorldToScreenPoint(worldPos);

    #region Animation
    private float _targetZoomInSize = 0;
    private float _targetZoomInDuration = 0.5f;
    private float _targetZoomInDelay = 0.5f;
    private float _targetZoomOutSize = 0;
    private float _targetZoomOutDuration = 0.5f;
    private Sequence _seqCameraZoom;

    private float CameraOrthographicSize
    {
        get => Camera.orthographicSize;
        set => Camera.orthographicSize = value;
    }

    public void StartGameCameraAnimation(Action onCompleteAnimation = null)
    {
        float startCameraSize = Camera.orthographicSize;
        float maxCameraSize = InputActions_CameraZoom.MaxZoom;
        Camera.orthographicSize = maxCameraSize;
        _seqCameraZoom.Kill();
        _seqCameraZoom = DOTween.Sequence();
        if (startCameraSize > maxCameraSize)
        {
            _targetZoomOutSize = startCameraSize;
            _targetZoomInSize = maxCameraSize;
            _seqCameraZoom.Append(DOTween.To(
                    () => CameraOrthographicSize,
                    x => CameraOrthographicSize = x,
                    _targetZoomOutSize, _targetZoomOutDuration));
        }
        else
        {
            _targetZoomInSize = startCameraSize;
        }
        _seqCameraZoom.AppendInterval(_targetZoomInDelay);
        _seqCameraZoom.Append(DOTween.To(
            () => CameraOrthographicSize,
            x => CameraOrthographicSize = x,
            _targetZoomInSize, _targetZoomInDuration));
        _seqCameraZoom.OnComplete(() => onCompleteAnimation?.Invoke());
    }
  #endregion
}
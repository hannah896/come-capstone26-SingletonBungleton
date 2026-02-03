using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 카메라의 이동, 크기 등을 관리해주는 매니저.
/// 게임 화면 비율에 맞춰 카메라를 조정합니다.
/// </summary>
public class ScreenManager : CoreManager
{
    #region Constants

    // 카메라 배경색
    private static readonly Color CameraColorBG = new Color(1, 1, 1, 1);

    // 카메라 Y 버퍼
    private static float CameraYBuffer = 10f;

    #endregion

    #region Fields

    // 메인 카메라 래퍼
    private MainCamera _mainCamera;

    // 줌인 애니메이션 설정
    private float _targetZoomInSize = 0;
    private float _targetZoomInDuration = 0.5f;
    private float _targetZoomInDelay = 0.5f;
    private float _targetZoomOutSize = 0;
    private float _targetZoomOutDuration = 0.5f;

    // 카메라 줌 시퀀스
    private Sequence _seqCameraZoom;

    // 최대 카메라 크기
    private float MaxCameraSize;

    #endregion

    #region Properties

    // 참조 해상도
    public Vector2 ReferenceResolution => new(1080f, 1920f);

    // 카메라 크기
    public Vector2 CameraSize { get; private set; }

    // 카메라 위치
    public Vector3 CameraPosition { get; private set; }

    // 화면 비율
    public float Aspect { get; private set; }

    // 역 화면 비율
    public float ReverseAspect { get; private set; }

    // 메인 카메라
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

    // Unity 카메라
    public Camera Camera => MainCamera.Camera;

    // 카메라 직교 크기
    private float CameraOrthographicSize
    {
        get => Camera.orthographicSize;
        set => Camera.orthographicSize = value;
    }

    #endregion

    #region Initialization

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

    #endregion

    #region Camera Setup

    /// <summary>
    /// 카메라를 설정합니다.
    /// </summary>
    public void SetCamera()
    {
        if (Main.Scene.Current is GameScene)
        {
            UI_Hud_Game uiHud = (Main.Scene.Current as GameScene)?.UIHud;
            float topUIRatio = uiHud.TopUIRatio;
            float bottomUIRatio = uiHud.BottomUIRatio;
            SetGameCamera(topUIRatio, bottomUIRatio);
            Main.Screen.MainCamera.SetColorCameraBG(CameraColorBG);
        }
    }

    // 게임 카메라 설정
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

    /// <summary>
    /// 월드 좌표를 스크린 좌표로 변환합니다.
    /// </summary>
    public Vector3 GetScreenPos(Vector3 worldPos) => Camera.WorldToScreenPoint(worldPos);

    #endregion

    #region Animation

    /// <summary>
    /// 게임 시작 카메라 애니메이션을 실행합니다.
    /// </summary>
    public void StartGameCameraAnimation(Action onCompleteAnimation = null)
    {
        float startCameraSize = Camera.orthographicSize;
        float maxCameraSize = MaxCameraSize;
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

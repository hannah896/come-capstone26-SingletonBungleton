using Blossom.Preference;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class GameScene : SceneBase
{
    #region Properties

    public static GameState GameState
    {
        get => _gameState;
        set
        {
            if (_gameState == value) return;
            _gameState = value;
            if (_gameState == GameState.InTutorial)
            {
                (Main.Scene.Current as GameScene)?.OnGameTutorial?.Invoke();
            }
            else if (_gameState == GameState.Playing)
            {
                GameEvents.OnGameStart?.Invoke();
                (Main.Scene.Current as GameScene)?.OnGameStart?.Invoke();
            }
            else if (_gameState == GameState.Success)
            {
                GameEvents.OnGameClear?.Invoke();
                if (Main.IsEditorMode)
                    Main.Scene.Load("EditorScene");
                else
                    (Main.Scene.Current as GameScene)?.SuccessGame();
            }
            else if (_gameState == GameState.Failed)
            {
                GameProcessing = GameProcessing.Stopping;
                GameEvents.OnGameOver?.Invoke();
                if (Main.IsEditorMode)
                    Main.Scene.Load("EditorScene");
                else
                    (Main.Scene.Current as GameScene)?.FailGame();
            }
        }
    }

    public static GameProcessing GameProcessing { get; set; }

    // public static ItemType CurrentItemType { get; set; }

    public static StageData CurrentStage { get; private set; }
    public UI_Hud_Game UIHud { get; private set; }

    #endregion

    #region Fields

    private static GameState _gameState = GameState.None;
    private const int MaxHeartCount = 3;

    public static int HeartCount
    {
        get => _heartCount;
        set
        {
            int setValue = value;
            if (IsInfinityHeart) setValue = MaxHeartCount;
            if (setValue == _heartCount) return;
            _heartCount = setValue;
            GameEvents.OnChangeHeart?.Invoke(setValue);
        }
    }

    private static int _heartCount;
    public static bool IsInfinityHeart = false;

    private PlayPrefs _playPrefs;

    private Coroutine _coEndGame;

    public event Action OnGameReady;
    public event Action OnGameTutorial;
    public event Action OnGameStart;

    #endregion

    #region MonoBehaviours

    private void OnDisable() { Main.Loop.ResetGameEvent(); }

    protected void Update()
    {
        Main.Time.OnUpdate(Time.deltaTime);

        // TEMP!
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_STANDALONE_WIN
        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            GameState = GameState.Success;
        }
        else if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            GameState = GameState.Failed;
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Main.IsEditorMode)
            {
                GameState = GameState.None;
                Main.Scene.Load("EditorScene");
            }
        }
#endif
    }

    #endregion

    #region Game

    public void StartGame(int stage = -1)
    {
        //Main.Ads.ShowInterstitialCheckSave(() =>
        //{
        //    ResetGame();
        //    InitGame(stage);
        //});
    }

    public void RetryGame()
    {
        int stage = CurrentStage.Index;
        StartGame(stage);
    }

    private void ResetGame()
    {
        Main.Clear();
        Main.Loop.ResetGameEvent();
        GameProcessing = GameProcessing.Stopping;
        RefillHeart();
        Main.Board.Clear();
    }

    public void RefillHeart() => HeartCount = MaxHeartCount;

    private async void InitGame(int stage = -1)
    {
        // #1. Stage 불러오기.
        if (Main.IsEditorMode)
            CurrentStage = Main.Data.EditorStageData;
        else
        {
            if (stage == -1) stage = _playPrefs.Stage.Value;
            SetStageData(stage);
        }

        // #2. 개체 생성.
        Main.Board.GenerateBoard(CurrentStage);

        // #3. 개체 오브젝트 생성.
        Main.Board.GenerateBoardObject();

        // #5. UI 생성.
        UIHud = Object.FindFirstObjectByType<UI_Hud_Game>();
        if (UIHud == null)
        {
            UIHud = await Extensions.ShowHud<UI_Hud_Game>();
        }

        // #6. 카메라 및 인풋 설정.
        Main.Screen.SetCamera();
        InputController.AllowInput = true;
        // Main.Input.SetInputActions(InputActionType.None);

        // #7. 게임 시작.
        GameEvents.OnGameReady?.Invoke();
        OnGameReady?.Invoke();
        GameState = GameState.Playing;
        GameProcessing = GameProcessing.Processing;

        UIHud.Set(this);
        // Main.Screen.StartGameCameraAnimation(() => Main.Input.SetInputActions(InputActionType.GameScenePlay));

        // #8. 튜토리얼 확인
        // foreach (TutorialLevelType type in Enum.GetValues(typeof(TutorialLevelType)))
        // {
        //     if (type == TutorialLevelType.None) continue;
        //     if (stage == (int)type)
        //     {
        //         UI_Panel_Tutorial panel = await Extensions.ShowPopup<UI_Panel_Tutorial>();
        //         panel.Set(type);
        //         break;
        //     }
        // }
    }

    public void SuccessGame()
    {
        if (GameState == GameState.Waiting) return;
        GameState = GameState.Waiting;

        Prefs.Get<PlayPrefs>().Stage.Value = CurrentStage.Index + 1;
        //Main.AnalyticsSDK.LogEvent($"rca_clear_{CurrentStage.Index:D4}", null, AnalyticsType.GF);
        CoSuccessGame();
    }

    private async void CoSuccessGame()
    {
        await UniTask.WaitForSeconds(1f);
        GameProcessing = GameProcessing.Stopping;
        //UI_Popup_StageCompleted popup = await Extensions.ShowPopup<UI_Popup_StageCompleted>();
        //popup.Set();
    }

    public void FailGame()
    {
        if (GameState == GameState.Waiting) return;
        GameState = GameState.Waiting;
        CoFailedGame();
    }

    private async void CoFailedGame()
    {
        await UniTask.WaitForSeconds(1f);

        //if (CurrentStage.Index < AdsManager.ShowInterstitialStage)
        //{
        //    UI_Popup_RetryFree popup = await Extensions.ShowPopup<UI_Popup_RetryFree>();
        //    popup.Set();
        //}
        //else
        //{
        //    UI_Popup_RetryAds popup = await Extensions.ShowPopup<UI_Popup_RetryAds>();
        //    popup.Set();
        //}
    }

    #endregion

    private void SetStageData(int stage)
    {
        if (stage == -1) CurrentStage = Main.Data.GetStageData(1);
        else CurrentStage = Main.Data.GetStageData(stage) ?? Main.Data.GetStageData(_playPrefs.Stage.Value - 1);
    }

    #region Events

    private void OnSecondGameTimer(NyoTimer timer) { }

    private void OnTimeEndGameTimer(NyoTimer timer)
    {
        if (Main.IsEditorMode) return;
        GameState = GameState.Failed;
    }

    #endregion

    public override UniTask EnterScene(CancellationToken token)
    {
        _playPrefs = Prefs.Get<PlayPrefs>();
        StartGame(_playPrefs.Stage.Value);
        return UniTask.CompletedTask;
    }

    public override void ExitScene()
    {
        return;
    }
}

public enum GameState
{
    None = -1,
    Ready = 0,
    InTutorial,
    Playing,
    Success,
    Failed,
    Waiting, // Success, Fail
}

public enum GameProcessing
{
    None = 0,
    Processing = 1 << 0,
    Stopping = 1 << 1,
}
using System;
using System.Text;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class UI_Editor : UI_Panel {

    #region Const

    public const string ActionFitPassword = "0304125";
    public static bool IsActiveEditor
    {
        get => _isActiveEditor;
        set
        {
            _isActiveEditor = value;
            OnChangeActiveEditor?.Invoke(value);
        }
    }
    private static bool _isActiveEditor;
    public static Action<bool> OnChangeActiveEditor;

    #endregion
    
    #region Fields

    private GameObject _objSetting;
    
    private TMP_InputField _inputPassword;
    private TMP_InputField _inputStage;
    private TMP_InputField _inputGold;

    private UI_Text _txtCameraMove;
    private UI_Text _txtCameraZoom;
    private UI_Text _txtAdRemove;
    private UI_Text _txtSendEvent;
    
    private Canvas _canvas;
    private int _clickEditorCount;
    
    private PlayPrefs _playPrefs;
    private CurrencyPrefs _currencyPrefs;
    
    #endregion

    protected void OnDisable()
    {
        GameEvents.OnGameReady -= ResetEditorClickCount;
        OnChangeActiveEditor -= SettingEditor;
    }

    #region Initialize

    public override bool Initialize() {
        if (!base.Initialize()) return false;
        
        _currencyPrefs = Prefs.Get<CurrencyPrefs>();
        _playPrefs = Prefs.Get<PlayPrefs>();

        _objSetting = gameObject.FindChild("SettingEditor");
        _inputPassword = gameObject.FindChild<TMP_InputField>("Input_Password");
        _inputStage = gameObject.FindChild<TMP_InputField>("Input_Stage");
        _inputGold = gameObject.FindChild<TMP_InputField>("Input_Gold");

        _txtCameraMove = gameObject.FindChild<UI_Text>("Txt_CameraMove");
        _txtCameraZoom = gameObject.FindChild<UI_Text>("Txt_CameraZoom");
        _txtAdRemove =  gameObject.FindChild<UI_Text>("Txt_AdRemove");
        _txtSendEvent = gameObject.FindChild<UI_Text>("Txt_SendClearEvent");
        
        _canvas = GetComponent<Canvas>();
        gameObject.FindChild<UI_Button>("Btn_PasswordEnter").SetEvent(OnEnterPassword);
        gameObject.FindChild<UI_Button>("Btn_StageEnter").SetEvent(OnEnterStage);
        gameObject.FindChild<UI_Button>("Btn_GoldEnter").SetEvent(OnEnterGold);
        gameObject.FindChild<UI_Button>("Btn_Close").SetEvent(OnEnterClose);
        gameObject.FindChild<UI_Button>("Btn_Failed").SetEvent(OnEnterFailed);

        gameObject.FindChild<UI_Button>("Btn_CameraMove").SetEvent(OnEnterCameraMove);
        gameObject.FindChild<UI_Button>("Btn_CameraZoom").SetEvent(OnEnterCameraZoom);
        //gameObject.FindChild<UI_Button>("Btn_AdRemove").SetEvent(OnEnterAdRemove);
        gameObject.FindChild<UI_Button>("Btn_SendClearEvent").SetEvent(OnEnterSendClearLog);

        _canvas.overrideSorting = true;
        _canvas.sortingLayerName = "UI";
        
        GameEvents.OnGameReady += ResetEditorClickCount;
        _objSetting.gameObject.SetActive(false);
        _inputPassword.gameObject.SetActive(false);
        _clickEditorCount = 0;
        
        SettingEditor(IsActiveEditor);
        OnChangeActiveEditor += SettingEditor;
        
        return true;
    }

    #endregion

    #region Events
    
    public void OnClickEditor()
    {
        Initialize();
        _clickEditorCount++;
        
        if (_clickEditorCount >= 20)
        {
            _clickEditorCount = 0;
            gameObject.SetActive(true);
            _inputPassword.gameObject.SetActive(true);
            _objSetting.gameObject.SetActive(false);
        }
    }

    private void OnEnterStage()
    {
        int stageNum = 1;
        if (!int.TryParse(_inputStage.text, out stageNum))
        {
            Debug.LogError($"Enter Stage Number is invalid, string : {_inputStage.text}");
            return;
        }
        StageData data = Main.Data.GetStageData(stageNum);
        if (data == null)
        {
            Debug.LogError($"Enter Stage Number is invalid, : {_inputStage.text}");
            return;
        }
        GameScene scene = Main.Scene.Current as GameScene;
        if (scene == null)
        {
            Debug.LogError($"scene is invalid");
            return;
        }
        scene.StartGame(stageNum).Forget();
    }

    private void OnEnterGold()
    {
        int gold = 0;
        if (!int.TryParse(_inputGold.text, out gold))
        {
            Debug.LogError($"Gold Number is invalid, : {_inputGold.text}");
            return;
        }
        _currencyPrefs.Currency.Value = gold;
    }

    private void OnEnterPassword()
    {
        if (ActionFitPassword == _inputPassword.text)
        {
            IsActiveEditor = true;
            _objSetting.gameObject.SetActive(true);
        }
        _inputPassword.text = string.Empty;
        _inputPassword.gameObject.SetActive(false);
    }
    
    private void ResetEditorClickCount() => _clickEditorCount = 0;
    private void SettingEditor(bool active) => _objSetting.gameObject.SetActive(active);
    private void OnEnterFailed()
    {
        GameScene scene = Main.Scene.Current as GameScene;
        if (scene == null) return;
        scene.FailGame();
    }
    private void OnEnterClose() => IsActiveEditor = false;
    private void OnEnterCameraMove()
    {
        // Main.Input.ToggleInputAction(InputActionType.CameraMove);
        // bool isActive = Main.Input.IsActiveAction(InputActionType.CameraMove);
        StringBuilder sb = new();
        sb.AppendLine("Camera");
        sb.AppendLine("Move");
        // sb.Append($"{GetActiveString(isActive)}");
        _txtCameraMove.Text = sb.ToString();
    }

    private void OnEnterCameraZoom()
    {
        // Main.Input.ToggleInputAction(InputActionType.CameraZoom);
        // bool isActive = Main.Input.IsActiveAction(InputActionType.CameraZoom);
        StringBuilder sb = new();
        sb.AppendLine("Camera");
        sb.AppendLine("Zoom");
        // sb.Append($"{GetActiveString(isActive)}");
        _txtCameraZoom.Text = sb.ToString();
    }

    private void OnEnterSendClearLog()
    {
        int maxStage = Main.Data.GetMaxStageCount();
        //for (int i = 1; i <= maxStage; i++)
        //{
        //    Main.AnalyticsSDK.LogEvent($"rca_clear_{i:D4}", null, AnalyticsType.GF);
        //}
        StringBuilder sb = new();
        sb.AppendLine("Send Clear");
        sb.AppendLine("Event Log");
        sb.Append($"[Success Log]");
        _txtSendEvent.Text = sb.ToString();
    }

    private string GetActiveString(bool active)
    {
        return active ? "Now:Connect" : "Now:Disconnect";
    }
    #endregion

}
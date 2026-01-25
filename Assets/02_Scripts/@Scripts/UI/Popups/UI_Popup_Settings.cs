using UnityEngine;

public class UI_Popup_Settings : UI_Popup {

    #region Fields

    private UI_Toggle _tgBGM;
    private UI_Toggle _tgSFX;
    private UI_Toggle _tgHaptic;
    private UI_Button _btnHome;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        // _tgBGM = this.gameObject.FindChild<UI_Toggle>("tgBGM").SetEvent(OnToggleBGM);
        // _tgSFX = this.gameObject.FindChild<UI_Toggle>("tgSFX").SetEvent(OnToggleSFX);
        // _tgHaptic = this.gameObject.FindChild<UI_Toggle>("tgHaptic").SetEvent(OnToggleHaptic);
        // this.gameObject.FindChild<UI_Button>("btnSupport").SetEvent(OnButtonSupport);
        this.gameObject.FindChild<UI_Button>("btnPrivacyPolicy").SetEvent(OnButtonPrivacyPolicy);
        this.gameObject.FindChild<UI_Button>("btnRestore").SetEvent(OnButtonRestore);
        _btnHome = this.gameObject.FindChild<UI_Button>("btnPlay").SetEvent(Close);
        this.gameObject.FindChild<UI_Button>("btnClose").SetEvent(Close);

        return true;
    }

    public void Set() {
        Initialize();

        // _tgBGM.Set(Main.Audio.OnBGM);
        // _tgSFX.Set(Main.Audio.OnSFX);
        // _tgHaptic.Set(Main.Audio.OnHaptic);
        _btnHome.gameObject.SetActive(Main.Scene.Current is GameScene);
    }

    #endregion

    #region Events

    // private void OnToggleBGM(bool value) => Main.Audio.OnBGM = value;
    // private void OnToggleSFX(bool value) => Main.Audio.OnSFX = value;
    // private void OnToggleHaptic(bool value) => Main.Audio.OnHaptic = value;

    private void OnButtonPrivacyPolicy() {
        SystemLanguage systemLanguage = Application.systemLanguage;
        string combinedURL = Def.URLPrivacyPolicy;
        combinedURL += systemLanguage switch {
            SystemLanguage.English => "-en",
            SystemLanguage.Korean => "-kr",
            SystemLanguage.Japanese => "-jp",
            _ => "-en"
        };
        Application.OpenURL(combinedURL);
    }

    private void OnButtonRestore() { }

    private void OnButtonSupport() => SupportMail.Contact();

    private void OnButtonHome() {
        // if (GameScene.GameState == GameState.Playing) {
        //     if (Main.Lives.IsUnlimited) Main.Scene.SwitchAsync("LobbyScene");
        //     else Main.UI.OpenPopup<UI_Popup_LeaveGame>().Set();
        // }
        // else Main.Scene.SwitchAsync("LobbyScene");

        Close();
    }
    
    #endregion

}
using Blossom.Preference;
using UnityEngine;

public class UI_Popup_LeaveGame : UI_Popup {

    #region Fields

    private PlayPrefs _playPrefs;
    private CurrencyPrefs _currencyPrefs;

    private UI_Text _txtLevel;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _txtLevel = this.gameObject.FindChild<UI_Text>("txtLevel");
        this.gameObject.FindChild<UI_Button>("btnLeave").SetEvent(OnButtonLeave);
        this.gameObject.FindChild<UI_Button>("btnClose").SetEvent(OnButtonClose);
        
        return true;
    }

    public void Set() {
        Initialize();
        _playPrefs = Prefs.Get<PlayPrefs>();
        _currencyPrefs = Prefs.Get<CurrencyPrefs>();
        
        // TODO:: Localization 적용하기.
        _txtLevel.Text = $"Level {_playPrefs.Stage.Value}";
        //_txtLevel.Text = Main.Locale.Get("General", "Level_", "level", $"{_playPrefs.Stage.Value}");
    }

    #endregion

    #region Events

    private void OnButtonLeave() {
        //if (Main.Lives.Use()) _currencyPrefs.Lives.DisplayValueSync();
        // Main.Scene.SwitchAsync("LobbyScene");
        //Main.Scene.SwitchAsync("GameScene");
        Close();
    }

    private void OnButtonClose() => Close();

    #endregion
    
}
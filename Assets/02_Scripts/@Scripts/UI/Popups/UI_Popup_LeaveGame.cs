using Blossom.Preference;
using UnityEngine;

public class UI_Popup_LeaveGame : UI_Popup {

    #region Fields

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        this.gameObject.FindChild<UI_Button>("btnLeave").SetEvent(OnButtonLeave);
        this.gameObject.FindChild<UI_Button>("btnClose").SetEvent(OnButtonClose);
        
        return true;
    }

    public void Set() {
        Initialize();
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
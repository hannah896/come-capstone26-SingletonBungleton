
using System.Collections;
using Blossom.Preference;
using UnityEngine;

public class UI_PageLobby : UI_Page
{
    #region Fields

    private UI_Button _btnStart;
    private UI_Text _txtStart;
    
    private PlayPrefs _playPrefs;

    #endregion
    
    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _playPrefs = Prefs.Get<PlayPrefs>();
        _txtStart = gameObject.FindChild<UI_Text>("Txt_StartBtn");
        _btnStart = gameObject.FindChild<UI_Button>("Btn_StartBtn");
        _btnStart.SetEvent(OnGameStart);
        
        return true;
    }

    protected override PageType GetPageType() => PageType.Lobby;

    public override void Set()
    {
        Initialize();
        
        _txtStart.Text = $"Level {_playPrefs.Stage.Value + 1}";
    }

    private void OnGameStart()
    {
        //Main.Lives.Update();
        //if (Main.Lives.UseHeart)
        //{
        //    LobbyScene.LobbyState = LobbyState.Start;
        //    Main.Scene.SwitchAsync("GameScene");
        //}
        //else
        //{
        //     Main.UI.OpenPopup<UI_Popup_LobbyRefillHeart>().Set();
        //}
    }
}

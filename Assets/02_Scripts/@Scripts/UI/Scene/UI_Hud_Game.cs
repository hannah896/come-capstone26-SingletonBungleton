using UnityEngine;

public class UI_Hud_Game : UI_Hud {
    
    #region Properties

    public GameScene Scene { get; private set; }
    public float TopUIRatio { get; private set; }
    public float BottomUIRatio { get; private set; }
    public UI_GameHUD_Top HUD_Top => _gameHUDTop;
    public UI_GameHUD_Bot HUD_Bot => _gameHUDBot;

    #endregion
    
    #region Fields

    private float _safeTopHeight;
    private float _safeBottomHeight;
    
    private UI_GameHUD_Top _gameHUDTop;
    private UI_GameHUD_Bot _gameHUDBot;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _gameHUDTop = this.gameObject.FindChild<UI_GameHUD_Top>("UI_GameHUD_Top");
        _gameHUDBot = this.gameObject.FindChild<UI_GameHUD_Bot>("UI_GameHUD_Bot");
        
        return true;
    }
    
    public void Set(GameScene scene) {
        Initialize();
        Scene = scene;
        //Main.Scene.SceneUI = this;
        
        _gameHUDTop.Set();
        _gameHUDBot.Set();
        // _gameButtons.Set();
    }
    
    #endregion
}
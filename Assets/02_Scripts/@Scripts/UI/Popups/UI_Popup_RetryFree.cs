using Blossom.Preference;
using UnityEngine;

public class UI_Popup_RetryFree : UI_Popup {
    #region Fields
    
    private GameScene _scene;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;
        
        _scene = Main.Scene.Current as  GameScene;

        this.gameObject.FindChild<UI_Button>("Btn_Retry").SetEvent(OnButtonRetry);
        this.gameObject.FindChild<UI_Button>("Btn_FreeRefill").SetEvent(OnButtonFreeRefill);
        
        return true;
    }

    public void Set() {
        Initialize();
    }

    #endregion

    #region Events

    private void OnButtonRetry() 
    {
        _scene.RetryGame();
        Close();
    }

    private void OnButtonFreeRefill()
    {
        ContinueGame();
    }

    private void ContinueGame()
    {
        Close();
        _scene.RefillHeart();
        GameScene.GameState = GameState.Playing;
        GameScene.GameProcessing = GameProcessing.Processing;
    }

    #endregion

}
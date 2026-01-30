using UnityEngine;

public class UI_GameScene : UI_Scene {
    
    #region Properties

    public GameScene Scene { get; private set; }
    public float TopUIRatio { get; private set; }
    public float BottomUIRatio { get; private set; }
    public UI_GameHUD_Top HUD_Top => _gameHUDTop;
    public UI_GameHUD_Bot HUD_Bot => _gameHUDBot;

    public Transform Parent => _safeArea.transform;

    #endregion
    
    #region Fields

    private float _safeTopHeight;
    private float _safeBottomHeight;
    
    private RectTransform _safeArea;
    private UI_GameHUD_Top _gameHUDTop;
    private UI_GameHUD_Bot _gameHUDBot;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _safeArea = this.gameObject.FindChild<RectTransform>("SafeArea");
        _gameHUDTop = this.gameObject.FindChild<UI_GameHUD_Top>("UI_GameHUD_Top");
        _gameHUDBot = this.gameObject.FindChild<UI_GameHUD_Bot>("UI_GameHUD_Bot");
        
        return true;
    }
    
    public void Set(GameScene scene) {
        Initialize();
        Scene = scene;
        Main.Scene.SceneUI = this;
        
        ApplySafeArea();
        CalculateUIRatio();
        
        _gameHUDTop.Set();
        _gameHUDBot.Set();
        // _gameButtons.Set();
    }
    
    #endregion
    
    private void ApplySafeArea() {
        Rect safe = Screen.safeArea;
        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;
        
        //float bannerHeight = Main.Ads.GetAdaptiveBannerHeightInPixels();
        //if (bannerHeight > 0)
        //{
        //    anchorMin.y += bannerHeight;
        //}
        
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _safeArea.anchorMin = anchorMin;
        _safeArea.anchorMax = anchorMax;
        _safeArea.offsetMin = Vector2.zero;
        _safeArea.offsetMax = Vector2.zero;

        _safeTopHeight = (1 - anchorMax.y) * Screen.height;
        _safeBottomHeight = anchorMin.y * Screen.height;
    }

    private void CalculateUIRatio() {
        //float totalHeight = (float)Screen.height;
        float totalHeight = Rect.rect.height;
        float topHeight = this.gameObject.FindChild<RectTransform>("TopArea").rect.height;
        float bottomHeight = this.gameObject.FindChild<RectTransform>("BottomArea").rect.height;
        TopUIRatio = (_safeTopHeight + topHeight) / totalHeight;
        BottomUIRatio = (_safeBottomHeight + bottomHeight) / totalHeight;
    }
    
}
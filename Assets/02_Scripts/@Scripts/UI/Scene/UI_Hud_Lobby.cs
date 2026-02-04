using UnityEngine.UI;

public class UI_Hud_Lobby : UI_Hud
{
    #region Fields & Properties

    public LobbyScene Scene { get; private set; }

    private UI_CurrencyInfo _currencyInfo;
    private UI_Button _infoGold;
    private UI_Editor _uiEditor;

    private GraphicRaycaster _grTop;
    private GraphicRaycaster _grPage;
    private GraphicRaycaster _grTab;

    #endregion

    #region Initialization / Set

    public override bool Initialize()
    {
        if (base.Initialize()) return false;
        
        _infoGold = gameObject.FindChild<UI_Button>("Btn_GoldIconPlus");
        //_tabController = gameObject.FindChild<UI_TabController>("UI_TabController");
        //_pageController = gameObject.FindChild<UI_PageController>("UI_Pages");
        //_livesInfo = gameObject.FindChild<UI_LivesInfo>("UI_LivesInfo");
        _currencyInfo =  gameObject.FindChild<UI_CurrencyInfo>("UI_CurrencyInfo");
        _uiEditor = gameObject.FindChild<UI_Editor>("UI_Editor");
        _grTop = gameObject.FindChild<GraphicRaycaster>("UI_Top");
        _grPage = gameObject.FindChild<GraphicRaycaster>("UI_Pages");
        _grTab = gameObject.FindChild<GraphicRaycaster>("UI_TabController");
        gameObject.FindChild<UI_Button>("Btn_Editor").SetEvent(_uiEditor.OnClickEditor);

        // NavigationMediator 생성 시 기본 페이지를 Lobby로 설정
        //_navigationMediator = new NavigationMediator(_tabController, _pageController);

        return true;
    }

    public void Set(LobbyScene scene)
    {
        Initialize();
        Scene = scene;
        ReadyGame();
        //Main.Scene.SceneUI = this;
        //_infoGold.SetEvent(() => _pageController.NavigateToPage(PageType.Shop));

        //_tabController.Set(Nav);
        //_pageController.Set(Nav);
        //_livesInfo.Set();
        Scene.OnLobbyStart += StartGame;
        Scene.OnLobbyReady += ReadyGame;
    }
    
    #endregion

    private void OnDestroy()
    {
        Scene.OnLobbyStart -= StartGame;
        Scene.OnLobbyReady -= ReadyGame;
    }

    private void StartGame()
    {
        _grTop.enabled = false;
        _grPage.enabled = false;
        _grTab.enabled = false;
    }

    private void ReadyGame()
    {
        _grTop.enabled = true;
        _grPage.enabled = true;
        _grTab.enabled = true;
    }
    
    private void ApplySafeArea() 
    {
        // Rect safe = Screen.safeArea;
        // Vector2 anchorMin = safe.position;
        // Vector2 anchorMax = safe.position + safe.size;
        // anchorMin.x /= Screen.width;
        // anchorMin.y /= Screen.height;
        // anchorMax.x /= Screen.width;
        // anchorMax.y /= Screen.height;
        //
        // _safeArea.anchorMin = anchorMin;
        // _safeArea.anchorMax = anchorMax;
        // _safeArea.offsetMin = Vector2.zero;
        // _safeArea.offsetMax = Vector2.zero;
    }
}
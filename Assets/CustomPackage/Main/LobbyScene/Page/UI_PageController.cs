
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UI_PageController : UI_Base
{
    #region Fields
    
    private readonly Dictionary<PageType, UI_Page> _pages = new();
    private SwipePageScrollRect _swipeScroll;
    private NavigationMediator _nav;
    private Canvas _canvas;
    
    public event Action<int, int> OnPageChangeStarted;
    public event Action<int, int> OnPageChangeCompleted;
    public event Action<LobbyScene> OnPageChangeCompletedLobby; 

    #endregion

    private void OnDestroy() => UnsubscribeEvents();

    public override bool Initialize()
    {
        if(!base.Initialize()) return false;

        _canvas = GetComponentInParent<Canvas>();
        _swipeScroll = gameObject.FindChild<SwipePageScrollRect>("SwipeScroll");
        _swipeScroll.InitializeComponents();
        
        CreatePages();
        ConfigureScrollRect();
        SubscribeEvents();

        // 홈페이지로 즉시 설정 (초기화 직후)
        var lobbyPageIndex = (int)PageType.Lobby;
        _swipeScroll.SnapToPage(lobbyPageIndex, immediate: true);

        return true;
    }

    public void Set(NavigationMediator mediator)
    {
        _nav = mediator;
        _canvas.worldCamera = Main.Screen.Camera;
        foreach(var page in _pages.Values)
        {
            page.Set();
        }
    }

    private void CreatePages()
    {
        var ePageTypes = Enum.GetValues(typeof(PageType)).Cast<PageType>();

        foreach (var ePageType in ePageTypes)
        {
            var page = CreatePageByType(ePageType);
            if (page)
            {
                _pages[page.Type] = page;
            }
        }

        foreach (var page in _pages.Values)
        {
            var targetIndex = (int)page.Type;
            page.transform.SetSiblingIndex(targetIndex);
            page.gameObject.name = $"Page_{page.Type}";
            page.Set(this, _canvas);
        }
    }

    private UI_Page CreatePageByType(PageType pageType)
    {
        var content = _swipeScroll.content;
        return pageType switch
        {
            PageType.Shop => Resources.Load<GameObject>($"prefabs/{nameof(UI_PageShop)}").Instantiate<UI_PageShop>(),
            PageType.Lobby => Resources.Load<GameObject>($"prefabs/{nameof(UI_PageLobby)}").Instantiate<UI_PageLobby>(),
            PageType.Lock => Resources.Load<GameObject>($"prefabs/{nameof(UI_PageLock)}").Instantiate<UI_PageLock>(),
            _ => null
        };
    }

    private void ConfigureScrollRect()
    {
        _swipeScroll.SetPageCount(_pages.Count);
        _swipeScroll.RefreshPageConfiguration();
    }
    
    private void SubscribeEvents()
    {
        _swipeScroll.OnPageChangeStarted += HandlePageChangeStarted;
        _swipeScroll.OnPageChangeCompleted += HandlePageChangeCompleted;
    }
    
    private void UnsubscribeEvents()
    {
        if (_swipeScroll)
        {
            _swipeScroll.OnPageChangeStarted -= HandlePageChangeStarted;
            _swipeScroll.OnPageChangeCompleted -= HandlePageChangeCompleted;
        }
        
        OnPageChangeStarted = null;
        OnPageChangeCompleted = null;
    }
    
    private void HandlePageChangeStarted(int prevPage, int tarPage)
    {
        OnPageChangeStarted?.Invoke(prevPage, tarPage);
        if (prevPage == tarPage) return;
        //Main.Haptic.Weak();
        // Main.JSAM.PlaySFX(AudioLibrarySounds.Pop1);
    }

    private void HandlePageChangeCompleted(int prevPage, int currPage) 
    {
        OnPageChangeCompletedLobby?.Invoke(Main.Scene.Current as LobbyScene);
        OnPageChangeCompleted?.Invoke(prevPage, currPage);
        _nav?.OnPageSwiped(currPage);
    }

    #region Public API

    public void NavigateToPage(int pageIndex, bool immediate = false) => _swipeScroll?.SnapToPage(pageIndex, immediate);
    public void NavigateToPage(PageType pageType, bool immediate = false) => NavigateToPage((int)pageType, immediate);
    public void NextPage(bool immediate = false) => _swipeScroll?.NextPage(immediate);
    public void PreviousPage(bool immediate = false) => _swipeScroll?.PreviousPage(immediate);
    public void GoToFirstPage(bool immediate = false) => _swipeScroll?.GotoFirstPage(immediate);
    public void GoToLastPage(bool immediate = false) => _swipeScroll?.GotoLastPage(immediate);
    public void SnapToNearestPage() => _swipeScroll?.SnapToNearestPage();
    public float GetPagePosition(int pageIndex) => _swipeScroll?.GetPagePosition(pageIndex) ?? 0f;
    public float GetInterPageProgress() => _swipeScroll?.GetInterPageProgress() ?? 0f;
    public UI_Page GetPage(PageType pageType) => _pages.TryGetValue(pageType, out var page) ? page : null;
    public T GetPage<T>(PageType pageType) where T : UI_Page => GetPage(pageType) as T;

    #endregion
}
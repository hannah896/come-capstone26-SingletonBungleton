
using System;
using Cysharp.Threading.Tasks;

public sealed class NavigationMediator : IDisposable
{
    #region Fields

    private readonly NavigationState _state;
    private UI_TabController _tabController;
    private UI_PageController _pageController;
    
    public PageType CurrentPage => _state.CurrentPage;

    #endregion

    #region Constructor & Initialization

    public NavigationMediator(UI_TabController tabController, UI_PageController pageController, PageType initialPage = PageType.Lobby)
    {
        _tabController = tabController;
        _pageController = pageController;
        
        _state = new NavigationState(initialPage);
        _state.OnPageChanged += HandleStateChanged;
    }
    #endregion

    public void NavigateTo(PageType targetPage, bool immediate = false)
    {
        if (immediate)
        {
            _state.ForceSetPage(targetPage);
            _tabController?.UpdateTabSelection(targetPage, true);
            _pageController?.NavigateToPage((int)targetPage, true);
        }
        else
        {
            var command = new NavigateToPageCommand(_state, targetPage);
            command.Execute();
        }
    }

    public void OnTabSelected(PageType pageType)
    {
        var command = new NavigateToPageCommand(_state, pageType);
        command.Execute();
    }

    public void OnPageSwiped(int pageIndex)
    {
        var ePageType = (PageType)pageIndex;
        _state.SetCurrentPage(ePageType);
    }

    private void HandleStateChanged(PageType previous, PageType current)
    {
        _tabController?.UpdateTabSelection(current, false);
        _pageController?.NavigateToPage((int)current, false);
    }

    public void Dispose()
    {
        if (_state != null)
        {
            _state.OnPageChanged -= HandleStateChanged;
        }
        
        _tabController = null;
        _pageController = null;
    }
}
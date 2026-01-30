using System;

public sealed class NavigationState
{
    #region Fields

    private PageType _currentPage;
    private bool _suppressEvents = false;
    
    public PageType CurrentPage => _currentPage;
    public event Action<PageType, PageType> OnPageChanged; 

    #endregion

    public NavigationState(PageType initialPage = PageType.Lobby)
    {
        _currentPage = initialPage;
    }

    public void SetCurrentPage(PageType newPage, bool suppressEvents = false)
    {
        if (_currentPage == newPage || _suppressEvents) return;
        
        var previousPage = _currentPage;
        _currentPage = newPage;
        
        if(!suppressEvents)
        {
            _suppressEvents = true;
            OnPageChanged?.Invoke(previousPage, _currentPage);
            _suppressEvents = false;
        }
    }
    
    public void ForceSetPage(PageType page) => _currentPage = page;
}
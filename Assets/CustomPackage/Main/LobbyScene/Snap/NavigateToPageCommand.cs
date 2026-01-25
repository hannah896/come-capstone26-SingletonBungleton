public readonly struct NavigateToPageCommand : INavigationCommand
{
    #region Fields

    private readonly NavigationState _state;
    private readonly PageType _targetPage;
    private readonly bool _immediate;

    #endregion

    public NavigateToPageCommand(NavigationState state, PageType targetPage, bool immediate = false)
    {
        _state = state;
        _targetPage = targetPage;
        _immediate = immediate;
    }
    
    public void Execute()
    {
        _state.SetCurrentPage(_targetPage);
    }
}
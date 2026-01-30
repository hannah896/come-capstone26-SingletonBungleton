using UnityEngine;

public sealed class PageNavigator
{
    #region Fields & Constructor

    private readonly PageCalculator _calculator;
    private readonly PageState _pageState;
    private readonly SnapConfig _snapConfig;

    public PageNavigator(PageCalculator calculator, PageState pageState, SnapConfig snapConfig)
    {
        _calculator = calculator ?? throw new System.ArgumentNullException(nameof(calculator));
        _pageState = pageState ?? throw new System.ArgumentNullException(nameof(pageState));
        _snapConfig = snapConfig ?? SnapConfig.Default;
    }

    #endregion

    public int DetermineTargetPage(DragState dragState)
    {
        var dragDistance = dragState.DragDistance;
        var absDragDistance = Mathf.Abs(dragDistance);

        if (absDragDistance >= _snapConfig.DragThreshold)
        {
            return dragDistance > 0
                ? Mathf.Min(_pageState.Current + 1, _calculator.PageCount - 1)
                : Mathf.Max(_pageState.Current - 1, 0);
        }

        return _calculator.GetNearestPageIndex(dragState.EndValue);
    }

    public bool CanNavigateToPage(int pageIndex)
    {
        return pageIndex >= 0 && pageIndex < _calculator.PageCount;
    }

    public bool ShouldPreventSnap(int pageIndex)
    {
        return _pageState.IsSnapping && _pageState.Target == pageIndex;
    }
}
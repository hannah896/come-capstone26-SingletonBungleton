
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UI_TabController : UI_Panel
{
    #region Fields
    
    private readonly Dictionary<PageType, UI_TabHandler> _tabHandlerMap = new();

    private NavigationMediator _nav;
    private UI_TabSelector _tabSelector;
    private RectTransform _tabContainer;

    private float _screenMatchWidth;

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        
        _tabSelector = gameObject.FindChild<UI_TabSelector>("UI_TabSelector");
        _tabContainer = gameObject.FindChild<RectTransform>("HLayout");

        BindTabHandlers();
        SetSelector();

        return true;
    }

    public void Set(NavigationMediator nav)
    {
        _nav = nav;
        SetSelectedHandlers();
        SetSelector();
    }

    private void SetSelectedHandlers()
    {
        foreach(var tabHandler in _tabHandlerMap.Values)
        {
            tabHandler.Set(this);
            tabHandler.gameObject.name = $"Tab_{tabHandler.PageType}";
            tabHandler.SetSelected(tabHandler.PageType == _nav.CurrentPage);
        }

        _screenMatchWidth = _tabHandlerMap.Values.Last().RectTrs.rect.width;
    }

    private void SetSelector()
    {
        if (!_tabSelector || _tabHandlerMap.Values.Count <= 0) return;
        
        _tabSelector.Set(_screenMatchWidth);
    }

    private void BindTabHandlers()
    {
        _tabHandlerMap.Clear();

        var tabHandlers = _tabContainer.GetComponentsInChildren<UI_TabHandler>();
        foreach (var tabHandler in tabHandlers)
        {
            _tabHandlerMap[tabHandler.PageType] = tabHandler;
        }
    }

    #endregion

    #region Selection

    public void OnTabSelected(PageType pageType)
    {
        _nav?.OnTabSelected(pageType);
    }

    public void UpdateTabSelection(PageType selectedTab, bool immediate = false)
    {
        foreach (UI_TabHandler handler in _tabHandlerMap.Values)
        {
            handler.SetSelected(handler.PageType == selectedTab);
        }
        
        _tabSelector?.MoveTo(selectedTab, immediate);
    }

    #endregion
}
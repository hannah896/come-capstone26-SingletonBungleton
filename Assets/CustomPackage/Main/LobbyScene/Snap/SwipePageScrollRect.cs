
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("UI/Swipe Page Scroll Rect", 38)]
[RequireComponent(typeof(RectTransform))]
public class SwipePageScrollRect : ScrollRect
{
    #region Fields

    [Header("Page Settings")] 
    [SerializeField] private int _pageCount = 1;
    [SerializeField] private SnapConfig _snapConfig = SnapConfig.Default;
    [SerializeField] private bool _autoCalculatePageCount = true;

    private PageCalculator _calculator;
    private PageNavigator _navigator;
    private SnapAnimator _snapAnimator;

    private PageState _pageState;
    private DragState _dragState;

    public int PageCount => _pageCount;
    public int CurrentPage => _pageState.Current;
    public int TargetPage => _pageState.Target;
    public bool IsSnapping => _pageState.IsSnapping;
    public bool IsDragging => _dragState.IsDragging;
    public float[] PagePositions => _calculator.PagePositions;
    
    private bool _isInitialized;

    public event Action<int, int> OnPageChangeStarted;
    public event Action<int, int> OnPageChangeCompleted;

    #endregion

    #region Unity Behavior

    protected override void Awake()
    {
        base.Awake();
        InitializeComponents();
    }

    protected override void Start()
    {
        base.Start();
        RefreshPageConfiguration();
    }

    protected override void OnDestroy()
    {
        _snapAnimator?.StopSnap();
        base.OnDestroy();
    }

    #endregion

    public void InitializeComponents()
    {
        if (_isInitialized) return;
        
        _pageState = new PageState();
        _dragState = new DragState();
        _calculator = new PageCalculator();
        _navigator = new PageNavigator(_calculator, _pageState, _snapConfig);
        _snapAnimator = new SnapAnimator(this, _snapConfig, _pageState, SetScrollValueImmediate);

        _isInitialized = true;
    }

    public void RefreshPageConfiguration()
    {
        if (_autoCalculatePageCount && content)
        {
            _pageCount = content.childCount;
        }

        _calculator.Initialize(_pageCount);
        _pageState.Reset();

        // 홈페이지(Lobby)로 기본 설정
        var lobbyPageIndex = (int)PageType.Lobby;
        if (_pageCount > lobbyPageIndex)
        {
            SetScrollValueImmediate(_calculator.GetPagePosition(lobbyPageIndex));
            _pageState.Current = lobbyPageIndex;
            _pageState.Target = lobbyPageIndex;
        }
        else if (_pageCount > 0)
        {
            SetScrollValueImmediate(_calculator.GetPagePosition(0));
            _pageState.Current = 0;
            _pageState.Target = 0;
        }
    }

    #region Scroll Rect Overrides

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _dragState.IsDragging = true;
        _dragState.StartValue = horizontalNormalizedPosition;
        _dragState.StartPosition = eventData.position;

        if (_pageState.IsSnapping)
        {
            _snapAnimator.StopSnap();
        }
        
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_dragState.IsDragging)
        {
            _dragState.LastPosition = eventData.position;
        }
        
        base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _dragState.IsDragging = false;
        _dragState.EndValue = horizontalNormalizedPosition;
        _dragState.LastPosition = eventData.position;
        
        PerformDirectionalSnap();
        
        base.OnEndDrag(eventData);
    }

    #endregion

    private void PerformDirectionalSnap()
    {
        if (_pageState.IsSnapping || _pageCount <= 1) return;
        
        var targetPageIndex = _navigator.DetermineTargetPage(_dragState);
        SnapToPage(targetPageIndex);
    }

    public void SnapToPage(int pageIndex, bool immediate = false)
    {
        if (!_navigator.CanNavigateToPage(pageIndex)) return;
        if (_navigator.ShouldPreventSnap(pageIndex)) return;
        
        var previousPage = _pageState.Current;
        _pageState.Target = pageIndex;
        
        OnPageChangeStarted?.Invoke(previousPage, _pageState.Target);

        var targetValue = _calculator.GetPagePosition(pageIndex);
        if (immediate)
        {
            SetScrollValueImmediate(targetValue);
            CompletePageChange(previousPage, pageIndex);
        }
        else
        {
            _snapAnimator.StartSnap(horizontalNormalizedPosition, targetValue,
                () => CompletePageChange(previousPage, pageIndex));
        }
    }
    
    private void CompletePageChange(int previousPage, int currentPage)
    {
        _pageState.Current = currentPage;

        OnPageChangeCompleted?.Invoke(previousPage, currentPage);
    }

    #region Public API

    public void SnapToNearestPage()
    {
        var nearestPageIndex = _calculator.GetNearestPageIndex(horizontalNormalizedPosition);
        SnapToPage(nearestPageIndex);
    }
    
    public void NextPage(bool immediate = false)
    {
        if (_pageState.Current < _pageCount - 1)
        {
            SnapToPage(_pageState.Current + 1, immediate);
        }
    }
    
    public void PreviousPage(bool immediate = false)
    {
        if (_pageState.Current > 0)
        {
            SnapToPage(_pageState.Current - 1, immediate);
        }
    }

    public void GotoFirstPage(bool immediate = false) => SnapToPage(0, immediate);
    public void GotoLastPage(bool immediate = false) => SnapToPage(_pageCount - 1, immediate);

    public float GetPagePosition(int pageIndex) => _calculator.GetPagePosition(pageIndex);
    public float GetInterPageProgress() => _calculator.GetInterPageProgress(horizontalNormalizedPosition);

    public void SetPageCount(int pageCount)
    {
        _pageCount = Mathf.Max(1, pageCount);
        RefreshPageConfiguration();
    }
    
    public void SetSnapConfig(SnapConfig snapConfig)
    {
        _snapConfig = snapConfig;
    }

    #endregion

    private void SetScrollValueImmediate(float value)
    {
        _pageState.IsManualNavigate = true;
        horizontalNormalizedPosition = Mathf.Clamp01(value);
        _pageState.IsManualNavigate = false;
    }
}

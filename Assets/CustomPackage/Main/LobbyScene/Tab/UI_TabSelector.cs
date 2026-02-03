
using DG.Tweening;
using UnityEngine;

public class UI_TabSelector : UI_Panel
{
    #region Fields & Properties

    [Header("Selector Settings")]
    [SerializeField] private float _animationDuration = 0.15f;
    
    private RectTransform _selectorTransform;
    private Tween _moveTween;
    private float _matchWidth;

    public bool IsAnimating => _moveTween != null && _moveTween.IsActive();

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _selectorTransform = GetComponent<RectTransform>();

        return true;
    }

    public void Set(float matchWidth)
    {
        Initialize();
        _matchWidth = matchWidth;
        SetSize(new Vector2(matchWidth, _selectorTransform.sizeDelta.y));
        SetInitialPosition();
    }

    private void SetInitialPosition()
    {
        if (_selectorTransform)
        {
            MoveTo((int)PageType.Lobby, true);
        }
    }

    #endregion

    #region Position Management
    
    public void MoveTo(int tabIndex, bool immediate = false)
    {
        var centerIndex = (int)PageType.Lobby;
        var relativeIndex = tabIndex - centerIndex; // 중심 기준 상대 위치 계산
        var targetPosX = _matchWidth * relativeIndex;
        var currentPos = _selectorTransform.anchoredPosition;
        
        _moveTween?.Kill();

        if (immediate)
        {
            _selectorTransform.anchoredPosition = new Vector2(targetPosX, currentPos.y);
        }
        else
        {
            _moveTween = _selectorTransform
                .DOAnchorPosX(targetPosX, _animationDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => _moveTween = null);
        }
    }
    
    public void MoveTo(PageType pageType, bool immediate = false)
    {
        var tabIndex = (int)pageType;
        MoveTo(tabIndex, immediate);
    }

    #endregion

    #region Public Interface
    
    public void StopAnimation()
    {
        _moveTween?.Kill();
        _moveTween = null;
    }
    
    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        StopAnimation();
    }

    #endregion
}
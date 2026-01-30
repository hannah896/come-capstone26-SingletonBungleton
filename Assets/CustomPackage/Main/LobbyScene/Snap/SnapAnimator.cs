
using System;
using System.Collections;
using UnityEngine;

public sealed class SnapAnimator
{
    #region Fields & Constructor

    private readonly MonoBehaviour _coroutineRunner;
    private readonly SnapConfig _snapConfig;
    private readonly PageState _pageState;
    private readonly Action<float> _setScrollValue;
    
    private Coroutine _snapCoroutine;
    
    public SnapAnimator(MonoBehaviour coroutineRunner, SnapConfig snapConfig, PageState pageState, Action<float> setScrollValue)
    {
        _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
        _snapConfig = snapConfig ?? SnapConfig.Default;
        _pageState = pageState ?? throw new ArgumentNullException(nameof(pageState));
        _setScrollValue = setScrollValue ?? throw new ArgumentNullException(nameof(setScrollValue));
    }

    #endregion
    
    public void StartSnap(float startValue, float targetValue, Action onComplete = null)
    {
        StopSnap();
        
        _pageState.IsSnapping = true;
        _snapCoroutine = _coroutineRunner.StartCoroutine(SnapCoroutine(startValue, targetValue, onComplete));
    }

    public void StopSnap()
    {
        if (_snapCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
        }

        _pageState.IsSnapping = false;
        _pageState.IsManualNavigate = false;
    }

    private IEnumerator SnapCoroutine(float startValue, float targetValue, Action onComplete)
    {
        var elapsed = 0f;
        _pageState.IsManualNavigate = true;

        while (elapsed < _snapConfig.Speed)
        {
            elapsed += Time.deltaTime;
            var progress = elapsed / _snapConfig.Speed;
            var easedProgress = _snapConfig.Curve.Evaluate(progress);
            
            var currentValue = Mathf.Lerp(startValue, targetValue, easedProgress);
            _setScrollValue(currentValue);

            yield return null;
        }

        _setScrollValue(targetValue);
        CompleteSnap(onComplete);
    }

    private void CompleteSnap(Action onComplete)
    {
        _pageState.IsSnapping = false;
        _pageState.IsManualNavigate = false;
        _snapCoroutine = null;
        onComplete?.Invoke();
    }
}

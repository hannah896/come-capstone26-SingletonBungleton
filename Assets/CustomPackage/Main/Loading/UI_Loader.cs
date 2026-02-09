using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UI_Loader : InitBehaviour {

    //#region Fields

    //private bool _isShowing;
    //private float _showTime;
    //private bool _hideFlag;
    
    //private Sequence _sequence;
    
    //// Components.
    //private CanvasGroup _canvasGroup;
    
    //private UI_Loading _currentLoading;
    //private Dictionary<ScreenEffectType, UI_Loading> _loadings = new();

    //#endregion

    //#region MonoBehaviours

    //private void Update() {
    //    if (!_isShowing) return;
    //    _showTime += Time.deltaTime;
    //}

    //#endregion

    //#region Initialize / Set

    //public override bool Initialize() {
    //    if (!base.Initialize()) return false;
        
    //    _loadings.Add(ScreenEffectType.Fade, gameObject.FindChild<UI_Loading_Transition>());
    //    _loadings.Add(ScreenEffectType.Ads, gameObject.FindChild<UI_Loading_Ads>());
    //    _loadings.Add(ScreenEffectType.Iap, gameObject.FindChild<UI_Loading_Iap>());
    //    DontDestroyOnLoad(gameObject);
        
    //    return true;
    //}

    //public void Set(ScreenEffectType type, Action onLoadingComplete = null)
    //{
    //    Initialize();
    //    _showTime = 0;
    //    _currentLoading = _loadings[type];
    //    _currentLoading.Set(onLoadingComplete);
    //}

    //#endregion

    //public void Show()
    //{
    //    if (_currentLoading == null)
    //    {
    //        Debug.LogError($"loading show is failed");
    //        return;
    //    }
    //    _isShowing = true;
    //    _currentLoading.FadeInLoading();
    //}

    //public void Hide(float minWaitSec = 3f) => StartCoroutine(HideCoroutine(minWaitSec));

    //private IEnumerator HideCoroutine(float minWaitSec = 3f)
    //{
    //    yield return new WaitUntil(() => _showTime >= minWaitSec);
    //    _currentLoading.FadeOutLoading();
    //    _isShowing = false;
    //}
}
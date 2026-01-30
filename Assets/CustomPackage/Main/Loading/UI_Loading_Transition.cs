using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class  UI_Loading_Transition : UI_Loading
{
    private const float FadeInDuration = 0.3f;
    private const float FadeOutDuration = 0.3f;

    private CanvasGroup _cg;
    
    public override bool Initialize()
    {
        if(!base.Initialize())return false;

        _cg = gameObject.GetComponent<CanvasGroup>();
        
        return true;
    }

    public override void Set(Action onLoadingComplete = null)
    {
        base.Set(onLoadingComplete);
        _cg.alpha = 1f;
    }

    public override void FadeInLoading()
    {
        base.FadeInLoading();
    } 

    public override void FadeOutLoading()
    { 
        base.FadeOutLoading();
        sequence.Append(_cg.DOFade(0, 0.5f));
    }
}

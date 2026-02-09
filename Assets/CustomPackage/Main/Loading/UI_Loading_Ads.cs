using DG.Tweening;
using System;
using UnityEngine;

public class UI_Loading_Ads : UI_Loading
{
    private CanvasGroup _cg;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        _cg = gameObject.GetOrAddComponent<CanvasGroup>();

        return true;
    }

    public override void Set(Action onLoadingComplete = null)
    {
        base.Set(onLoadingComplete);
        _cg.alpha = 1;
    }

    public override void FadeInLoading()
    {
        base.FadeInLoading();
    }

    public override void FadeOutLoading()
    {
        base.FadeOutLoading();
        sequence.Append(_cg.DOFade(0, 0.2f));
    }
}

using System;
using DG.Tweening;
using UnityEngine;

public class UI_Loading_Ads : UI_Loading
{
    private UI_Image _imgDim;
    private CanvasGroup _cg;
    
    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _imgDim = gameObject.FindChild<UI_Image>("Img_Dim");
        _cg = gameObject.GetComponent<CanvasGroup>();

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

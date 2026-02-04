using DG.Tweening;
using System;
using UnityEngine;

public class UI_Screen_Fade : UI_Screen
{
    private UI_Image _image;
    private CanvasGroup _cg;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        _image = gameObject.GetComponent<UI_Image>();
        _cg = gameObject.GetOrAddComponent<CanvasGroup>();

        _image.SetColor(Color.black);
        return true;
    }

    public override void Set(Action onLoadingComplete = null)
    {
        base.Set(onLoadingComplete);
        _cg.alpha = 0f;
    }

    public override void FadeInLoading()
    {
        base.FadeInLoading();
        sequence.Append(_cg.DOFade(1, 0.5f));
    }

    public override void FadeOutLoading()
    {
        base.FadeOutLoading();
        sequence.Append(_cg.DOFade(0, 0.5f));
    }
}

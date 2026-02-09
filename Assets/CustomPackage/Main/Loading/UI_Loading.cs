using DG.Tweening;
using System;
using UnityEngine;

public abstract class UI_Loading : InitBehaviour
{
    protected Sequence sequence;
    protected Action onLoadingComplete;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        return true;
    }

    public virtual void Set(Action _onLoadingComplete = null)
    {
        Initialize();
        onLoadingComplete = _onLoadingComplete;
        sequence?.Kill();
    }

    public virtual void FadeInLoading()
    {
        gameObject.SetActive(true);
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.OnComplete(() =>
        {
            onLoadingComplete?.Invoke();
        });
    }

    public virtual void FadeOutLoading()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.OnComplete(() => gameObject.SetActive(false));
    }
}

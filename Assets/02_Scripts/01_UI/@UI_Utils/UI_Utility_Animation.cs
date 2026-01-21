using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public enum PopupAnimationType
{
    None,
    Scale,
    Fade,
    SlideFromTop,
    SlideFromBottom,
    SlideFromLeft,
    SlideFromRight
}

public static class UI_Utility_Animation
{
    public const float ANIM_DURATION = 0.25f;

    private static Vector2 ScreenSize => new Vector2(Screen.width, Screen.height);

    /// <summary>
    /// 팝업 열기 애니메이션 통합 관리
    /// </summary>
    public static async UniTask PlayOpen(this UI_Popup popup)
    {
        Transform tr = popup.transform;
        PopupAnimationType type = popup.AnimationType;

        tr.DOKill();

        switch (type)
        {
            case PopupAnimationType.Scale:
                tr.localScale = Vector3.zero;
                await tr.DOScale(Vector3.one, ANIM_DURATION)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .AsyncWaitForCompletion();
                break;

            case PopupAnimationType.Fade:
                if (tr.TryGetComponent(out CanvasGroup group))
                {
                    group.alpha = 0f;
                    await group.DOFade(1f, ANIM_DURATION)
                        .SetUpdate(true)
                        .AsyncWaitForCompletion();
                }
                break;

            case PopupAnimationType.SlideFromTop:
                await PlaySlide(tr, new Vector3(0, ScreenSize.y, 0), Vector3.zero, Ease.OutQuad);
                break;

            case PopupAnimationType.SlideFromBottom:
                await PlaySlide(tr, new Vector3(0, -ScreenSize.y, 0), Vector3.zero, Ease.OutQuad);
                break;

            case PopupAnimationType.SlideFromLeft:
                await PlaySlide(tr, new Vector3(-ScreenSize.x, 0, 0), Vector3.zero, Ease.OutQuad);
                break;

            case PopupAnimationType.SlideFromRight:
                await PlaySlide(tr, new Vector3(ScreenSize.x, 0, 0), Vector3.zero, Ease.OutQuad);
                break;

            case PopupAnimationType.None:
            default:
                tr.localScale = Vector3.one;
                break;
        }
    }

    /// <summary>
    /// 팝업 닫기 애니메이션 통합 관리
    /// </summary>
    public static async UniTask PlayClose(this UI_Popup popup)
    {
        Transform tr = popup.transform;
        PopupAnimationType type = popup.AnimationType;

        tr.DOKill();

        switch (type)
        {
            case PopupAnimationType.Scale:
                await tr.DOScale(Vector3.zero, ANIM_DURATION)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true)
                    .AsyncWaitForCompletion();
                break;

            case PopupAnimationType.Fade:
                if (tr.TryGetComponent(out CanvasGroup group))
                {
                    await group.DOFade(0f, ANIM_DURATION)
                        .SetUpdate(true)
                        .AsyncWaitForCompletion();
                }
                break;

            case PopupAnimationType.SlideFromTop:
                await PlaySlide(tr, Vector3.zero, new Vector3(0, ScreenSize.y, 0), Ease.InQuad);
                break;

            case PopupAnimationType.SlideFromBottom:
                await PlaySlide(tr, Vector3.zero, new Vector3(0, -ScreenSize.y, 0), Ease.InQuad);
                break;

            case PopupAnimationType.SlideFromLeft:
                await PlaySlide(tr, Vector3.zero, new Vector3(-ScreenSize.x, 0, 0), Ease.InQuad);
                break;

            case PopupAnimationType.SlideFromRight:
                await PlaySlide(tr, Vector3.zero, new Vector3(ScreenSize.x, 0, 0), Ease.InQuad);
                break;

            case PopupAnimationType.None:
            default:
                break;
        }
    }

    private static async UniTask PlaySlide(Transform tr, Vector3 startView, Vector3 end, Ease ease)
    {
        tr.localPosition = startView;
        await tr.DOLocalMove(end, ANIM_DURATION)
            .SetEase(ease)
            .SetUpdate(true)
            .AsyncWaitForCompletion();
    }
}
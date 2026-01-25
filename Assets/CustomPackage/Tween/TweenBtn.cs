using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class TweenBtn : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private TweenDatas _downTweenData = new()
    {
        Ease = Ease.Linear,
        EndValue = 0.9f, 
        Duration = 0.1f,
        IsRelative = false,
        IsSpeedBased = false,
    };
    
    [SerializeField] private TweenDatas _upTweenData = new()
    {
        Ease = Ease.Linear,
        EndValue = 1f, 
        Duration = 0.1f,
        IsRelative = false,
        IsSpeedBased = false,
    };

    [SerializeField] private bool isClickSound = true;
    [SerializeField] private bool isHaptic = true;
    
    public void OnPointerDown(PointerEventData eventData)
    {
        Transform tr = _downTweenData.SelectTr ?? transform;
        tr.DOKill();
        tr.DOScale(_downTweenData.EndValue, _downTweenData.Duration)
            .SetEase(_downTweenData.Ease)
            .SetRelative(_downTweenData.IsRelative)
            .SetSpeedBased(_downTweenData.IsSpeedBased);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Transform tr = _upTweenData.SelectTr ?? transform;
        tr.DOKill();
        tr.DOScale(_upTweenData.EndValue, _upTweenData.Duration)
            .SetEase(_upTweenData.Ease)
            .SetRelative(_upTweenData.IsRelative)
            .SetSpeedBased(_upTweenData.IsSpeedBased);

        if (isClickSound)
        {
            // Main.JSAM.PlaySFX(AudioLibrarySounds.Pop1);
        }
    }
}

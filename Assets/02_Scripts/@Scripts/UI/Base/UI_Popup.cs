using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class UI_Popup : UI_Panel
{
    [HideInInspector] public UnityEvent OnDestroyEvent = new();
    protected Canvas Canvas;
    public PopupAnimationType AnimationType = PopupAnimationType.None;

    /// <summary>열릴 때 효과음. 소리가 없어야 하는 팝업(로딩 등)은 null로, 다른 소리는 오버라이드로 바꾼다.</summary>
    protected virtual AudioLibrarySounds? OpenSound => AudioLibrarySounds.UIOpen;

    /// <summary>닫힐 때 효과음. 규칙은 OpenSound와 같다.</summary>
    protected virtual AudioLibrarySounds? CloseSound => AudioLibrarySounds.UIClose;

    private bool _closeSoundPlayed;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        Canvas = GetComponentInParent<Canvas>();

        return true;
    }

    protected override void Start()
    {
        base.Start();
        if (OpenSound.HasValue) Extensions.PlaySFX(OpenSound.Value);
        this.PlayOpen().Forget();
    }

    public override void Close()
    {
        PlayCloseSoundOnce();
        CloseTask().Forget();
    }

    /// <summary>
    /// 닫기 효과음을 한 번만 재생한다. 애니메이션 닫기(Close)와 즉시 닫기(UIManager.ClosePopup) 양쪽에서 불려도 중복되지 않는다.
    /// 씬 전환 때 한꺼번에 지우는 경로(CloseAllPopups)는 부르지 않으므로 소리가 나지 않는다.
    /// </summary>
    public void PlayCloseSoundOnce()
    {
        if (_closeSoundPlayed) return;
        _closeSoundPlayed = true;
        if (CloseSound.HasValue) Extensions.PlaySFX(CloseSound.Value);
    }

    private async UniTask CloseTask()
    {
        base.Close();
        await this.PlayClose();

        if (Main.Instance != null && Main.UI != null)
        {
            Main.UI.ClosePopup(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        OnDestroyEvent?.Invoke();
    }
}

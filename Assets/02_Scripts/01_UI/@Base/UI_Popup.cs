using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.Events;

public class UI_Popup : UI_Base
{
    public UnityEvent OnDestroyEvent = new();
    public PopupAnimationType AnimationType = PopupAnimationType.None;

    protected override void Start()
    {
        base.Start();
        this.PlayOpen().Forget();
    }

    public override void Close()
    {
        CloseTask().Forget();
    }

    private async UniTask CloseTask()
    {
        base.Close();
        await this.PlayClose();

        if (Managers.Instance != null && Managers.UI != null)
        {
            Managers.UI.ClosePopup(this);
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
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public class UI_Popup : UI_Panel
{
    [HideInInspector] public UnityEvent OnDestroyEvent = new();
    protected Canvas Canvas;
    public PopupAnimationType AnimationType = PopupAnimationType.None;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        Canvas = GetComponentInParent<Canvas>();
        
        return true;
    }

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
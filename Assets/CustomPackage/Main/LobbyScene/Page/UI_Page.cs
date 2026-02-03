using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class UI_Page : UI_Panel
{
    #region Fields

    private PageType pageType;
    
    protected Canvas ParentCanvas { get; set; }
    protected Canvas Canvas { get; set; }
    protected CanvasGroup CanvasGroup { get; set; }
    public UI_PageController Controller { get; private set; }
    public PageType Type => pageType;

    #endregion
    
    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        pageType = GetPageType();
        if (!TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        Canvas = gameObject.GetComponent<Canvas>();
        CanvasGroup = canvasGroup;

        return true;
    }
    
    protected abstract PageType GetPageType();
    
    public void Set(UI_PageController controller, Canvas parentCanvas)
    {
        Controller = controller;
        ParentCanvas = parentCanvas;
        
        if (ParentCanvas.transform is not RectTransform parentRect)
        {
            return;
        }
        
        var layoutElement = GetComponent<LayoutElement>();
        if (!layoutElement)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        
        SetLayoutElement(layoutElement, parentRect);
    }

    private async void SetLayoutElement(LayoutElement layoutElement, RectTransform rectTransform)
    {
        await UniTask.Yield();
        
        layoutElement.preferredWidth = rectTransform.rect.width;
        layoutElement.flexibleWidth = 0;
        
        gameObject.SetActive(true);
    }
    
    public virtual void Set() { }
}
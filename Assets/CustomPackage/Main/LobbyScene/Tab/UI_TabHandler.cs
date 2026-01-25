using UnityEngine;

public class UI_TabHandler : UI_Button
{
    #region Fields & Properties

    [SerializeField] private PageType pageType;
    
    private UI_TabController _tabCtr;
    private UI_Text _textDesc;
    private UI_Image _imgIcon;

    public RectTransform RectTrs => transform as RectTransform;
    public PageType PageType => pageType;
    public bool IsSelected { get; private set; }

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if(!base.Initialize()) return false;
        
        _textDesc = this.gameObject.FindChild<UI_Text>("Txt_Desc");
        _imgIcon = this.gameObject.FindChild<UI_Image>("Img_Icon");

        return true;
    }

    public void Set(UI_TabController tabCtr)
    {
        _tabCtr = tabCtr;
        
        SetEvent(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        _tabCtr.OnTabSelected(pageType);
    }

    #endregion

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        string onOff =  selected ? "On" : "Off";
        _imgIcon.Sprite = Resources.Load<Sprite>($"Icon_Lobby_{PageType}_{onOff}");
        float SetSizeHeight = selected ? 330 : 240;
        SetSize(new Vector2(RectTrs.rect.width, SetSizeHeight));
        _textDesc.gameObject.SetActive(IsSelected);
    }
}
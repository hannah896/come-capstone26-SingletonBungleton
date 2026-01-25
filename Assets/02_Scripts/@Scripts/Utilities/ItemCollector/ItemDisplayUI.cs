using System;
using UnityEngine;

public class ItemDisplayUI : ItemDisplay {

    #region Fields

    [SerializeField] private Sprite[] _sprites;
    
    // Components.
    private Transform _overlayUI;
    private UI_Image _imgIcon;
    private UI_Text _txtAmount;
    
    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _overlayUI = GameObject.Find("UI_Overlay").transform;
        _imgIcon = this.gameObject.FindChild<UI_Image>();
        _txtAmount = this.gameObject.FindChild<UI_Text>();
        
        return true;
    }

    public override void Set(CollectorItem item, Vector3 position) {
        base.Set(item, position);

        //this.transform.SetParent(Main.Scene.SceneUI.transform);
        this.transform.SetParent(_overlayUI.transform);
        this.transform.position = position;

        // string spriteKey = item.Type switch {
        //     CollectorItemType.Clock => "Icon_Item_Clock",
        //     CollectorItemType.Balloon => "Icon_Item_Balloon",
        //     CollectorItemType.Churu => "Icon_Item_Churu",
        //     _ => $"Icon_{item.Type}"
        // };
        // _imgIcon.Sprite = Main.Resource.Get<Sprite>(spriteKey);
        _imgIcon.Sprite = _sprites[(int)item.Type];
        SetAmount();
    }

    private void SetAmount() {
        if (_txtAmount == null) return;

        bool showText = Item.Type switch {
            CollectorItemType.Currency => Item.Amount >= 100,
            CollectorItemType.UnlimitedLives => Item.Amount >= 1,
            _ => Item.Amount >= 5
        };

        if (Item.Type == CollectorItemType.UnlimitedLives) {
            _txtAmount.Text = showText ? $"{Item.Amount}m" : string.Empty;
        }
        else {
            _txtAmount.Text = showText ? $"x{Item.Amount}" : string.Empty;
        }
    }

    #endregion

}
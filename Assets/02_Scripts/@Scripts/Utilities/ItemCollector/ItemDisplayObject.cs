using TMPro;
using UnityEngine;

public class ItemDisplayObject : ItemDisplay {
    
    #region Fields
    
    // Components.
    private SpriteRenderer _spriter;
    private TextMeshPro _txtAmount;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _spriter = this.gameObject.FindChild<SpriteRenderer>();
        _txtAmount = this.gameObject.FindChild<TextMeshPro>();
        
        return true;
    }

    public override async void Set(CollectorItem item, Vector3 position) {
        base.Set(item, position);
        
        this.transform.position = position;
        _spriter.sprite = await Main.Resource.LoadAssetAsync<Sprite>($"Icon_{item.Type}");
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
            _txtAmount.text = showText ? $"{Item.Amount}m" : string.Empty;
        }
        else {
            _txtAmount.text = showText ? $"x{Item.Amount}" : string.Empty;
        }
    }

    #endregion

}
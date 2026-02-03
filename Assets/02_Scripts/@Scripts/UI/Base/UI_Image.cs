using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UI_Image : UI
{

    #region Properties

    public Sprite Sprite {
        get => _image.sprite;
        set {
            Initialize();
            _image.sprite = value;
            if (value == null) _image.color = Color.clear;
            else _image.color = Color.white;
        }
    }

    public Image Image => _image;

    #endregion

    #region Fields

    protected Image _image;

    #endregion

    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _image = this.GetComponent<Image>();

        return true;
    }

    public UI_Image SetColor(Color color) {
        Initialize();
        if (_image == null) return this;
        _image.color = color;
        return this;
    }

    public UI_Image SetFill(float amount) {
        Initialize();
        _image.fillAmount = amount;
        return this;
    }

    public UI_Image ResizeToVertical()
    {
        Initialize();
        if (_image.sprite == null) return this;

        float width = Rect.rect.width;
        float originalWidth = _image.sprite.rect.width;
        float originalHeight = _image.sprite.rect.height;

        float ratio = width / originalWidth;
        float newHeight = originalHeight * ratio;

        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
        return this;
    }

    public UI_Image ResizeToHorizontal()
    {
        Initialize();
        if (_image.sprite == null) return this;

        float height = Rect.rect.height;
        float originalWidth = _image.sprite.rect.width;
        float originalHeight = _image.sprite.rect.height;

        float ratio = height / originalHeight;
        float newWidth = originalWidth * ratio;

        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
        return this;
    }

    #endregion

}
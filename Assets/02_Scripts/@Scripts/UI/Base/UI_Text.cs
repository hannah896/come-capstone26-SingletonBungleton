using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UI_Text : UI_Base
{
    private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");

    #region Properties

    public string Text
    {
        get
        {
            Initialize();
            return _txt.text;
        }
        set
        {
            Initialize();
            if (isResizeText) ResizeText(value);
            string temp = value;
            if (OnChangeText != null) temp = OnChangeText?.Invoke(value);
            else _txt.text = value;
        }
    }

    public Color Color
    {
        get
        {
            Initialize();
            return _txt.color;
        }
    }

    public TMP_Text TMP
    {
        get
        {
            if (_txt == null) Initialize();
            return _txt;
        }
    }

    public Material DefaultMat
    {
        get
        {
            Initialize();
            return _defaultMat;
        }
    }

    #endregion

    #region Fields

    public TMP_Text TMPText => _txt;
    protected TMP_Text _txt;
    protected RectTransform _setParent;
    protected Material _defaultMat;
    [SerializeField] private bool isResizeText = false;
    [SerializeField] private float resizePadding;
    [SerializeField, SearchableEnum] protected ELocalizedName localeName = ELocalizedName.NONE;
    [SerializeField] public Color outLineColor = Color.white;
    public Func<string, string> OnChangeText;

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        if (!TryGetComponent(out _txt))
        {
            Debug.LogError("UI_Text not found");
        }

        if (isResizeText && !transform.parent.TryGetComponent(out _setParent))
        {
            Debug.LogError("parent RectTransform not found");
        }

        _defaultMat = _txt.fontMaterial;
        SetOutlineColor(outLineColor);

        return true;
    }

    public UI_Text SetSize(int size)
    {
        Initialize();

        if (size < 0) _txt.enableAutoSizing = true;
        else _txt.fontSize = size;

        return this;
    }

    public UI_Text SetColor(Color color)
    {
        Initialize();

        _txt.color = color;
        SetOutlineColor(outLineColor);

        return this;
    }

    public UI_Text SetOutlineColor(Color color)
    {
        Initialize();

        outLineColor = color;
        bool areRGBEqual = Color.r == outLineColor.r &&
                           Color.g == outLineColor.g &&
                           Color.b == outLineColor.b;
        
        if (!areRGBEqual)
        {
            Main.Text.SetOutlineColor(this);
        }
        else
        {
            SetFontMaterial(_defaultMat);
        }

        return this;
    }

    public UI_Text SetFont(TMP_FontAsset font)
    {
        Initialize();

        _txt.font = font;

        return this;
    }

    public UI_Text SetFontMaterial(Material material)
    {
        Initialize();

        _txt.fontMaterial = material;

        return this;
    }

    public UI_Text SetAlignment(TextAlignmentOptions alignment)
    {
        Initialize();

        _txt.alignment = alignment;

        return this;
    }

    private void ResizeText(string text, float maxWidth = Mathf.Infinity)
    {
        if (text == _txt.text) return;

        Vector2 size = _txt.GetPreferredValues(text, maxWidth, Mathf.Infinity);
        RectTransform rect = _txt.rectTransform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        Vector2 sizeDelta = rect.sizeDelta;
        if (sizeDelta.x < sizeDelta.y) sizeDelta.x = sizeDelta.y;
        _txt.text = text;

        if (_setParent == null)
        {
            Debug.LogError("_setParent not found");
            return;
        }

        _setParent.sizeDelta = sizeDelta + (Vector2.one * resizePadding);
    }

    #endregion
}
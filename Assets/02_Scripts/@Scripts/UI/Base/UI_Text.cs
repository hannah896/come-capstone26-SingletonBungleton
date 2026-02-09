using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using TMPro;

/// <summary>
/// TextMeshPro 텍스트를 관리하는 UI 컴포넌트.
/// 외곽선 설정, 로컬라이제이션, 자동 리사이즈 기능을 제공합니다.
/// </summary>
public class UI_Text : UI
{
    #region Fields

    // TMP 텍스트 컴포넌트
    protected TMP_Text _txt;

    // 기본 폰트 머티리얼
    protected Material _defaultMat;

    // 리사이즈용 부모 RectTransform
    protected RectTransform _resizeParent;

    // 로컬라이제이션 이벤트 구독 여부
    private bool _isLanguageEventSubscribed;

    // 로컬라이제이션 포맷 인자
    private object[] _formatArgs;

    [Header("Text Settings")]
    [SerializeField] private bool _autoResize;
    [SerializeField] private float _resizePadding;

    [Header("Localization")]
    [SerializeField, SearchableEnum]
    private ELocalizedName _localeName = ELocalizedName.NONE;

    [Header("Outline")]
    [SerializeField]
    private OutlineSettings _outlineSettings = new(Color.white, 0f, 0f);

    // 텍스트 변경 시 호출되는 콜백
    public Func<string, string> OnChangeText;

    #endregion

    #region Properties

    /// <summary>
    /// 텍스트 내용을 가져오거나 설정합니다.
    /// </summary>
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
            SetTextInternal(value);
        }
    }

    /// <summary>
    /// 텍스트 색상을 가져옵니다.
    /// </summary>
    public Color Color
    {
        get
        {
            Initialize();
            return _txt.color;
        }
    }

    /// <summary>
    /// 로컬라이제이션 키를 가져오거나 설정합니다.
    /// </summary>
    public ELocalizedName LocaleName
    {
        get => _localeName;
        set
        {
            _localeName = value;
            ApplyLocalization();
        }
    }

    /// <summary>
    /// 외곽선 설정을 가져옵니다.
    /// </summary>
    public OutlineSettings OutlineSettings => _outlineSettings;

    /// <summary>
    /// TMP 컴포넌트를 가져옵니다.
    /// </summary>
    public TMP_Text TMP
    {
        get
        {
            if (_txt == null) Initialize();
            return _txt;
        }
    }

    /// <summary>
    /// 기본 폰트 머티리얼을 가져옵니다.
    /// </summary>
    public Material DefaultMat
    {
        get
        {
            Initialize();
            return _defaultMat;
        }
    }

    #endregion

    #region Initialization

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        if (!TryGetComponent(out _txt))
        {
            Debug.LogError($"[UI_Text] TMP_Text component not found: {gameObject.name}");
            return false;
        }

        if (_autoResize && !transform.parent.TryGetComponent(out _resizeParent))
        {
            Debug.LogError($"[UI_Text] Parent RectTransform not found for auto resize: {gameObject.name}");
        }

        _defaultMat = _txt.fontMaterial;

        SubscribeLanguageEvent();
        ApplyOutline();
        ApplyLocalization();

        return true;
    }

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        SubscribeLanguageEvent();
        ApplyLocalization();
    }

    private void OnDisable()
    {
        UnsubscribeLanguageEvent();
    }

    private void OnDestroy()
    {
        UnsubscribeLanguageEvent();
    }

    #endregion

    #region Text Setting

    /// <summary>
    /// 텍스트 크기를 설정합니다. 음수면 자동 크기 조절을 활성화합니다.
    /// </summary>
    public UI_Text SetSize(int size)
    {
        Initialize();

        if (size < 0)
            _txt.enableAutoSizing = true;
        else
            _txt.fontSize = size;

        return this;
    }

    /// <summary>
    /// 텍스트 색상을 설정합니다.
    /// </summary>
    public UI_Text SetColor(Color color)
    {
        Initialize();

        _txt.color = color;
        ApplyOutline();

        return this;
    }

    /// <summary>
    /// 텍스트 정렬을 설정합니다.
    /// </summary>
    public UI_Text SetAlignment(TextAlignmentOptions alignment)
    {
        Initialize();

        _txt.alignment = alignment;

        return this;
    }

    #endregion

    #region Outline Setting

    /// <summary>
    /// 외곽선 색상을 설정합니다.
    /// </summary>
    public UI_Text SetOutlineColor(Color color)
    {
        Initialize();
        _outlineSettings.color = color;
        ApplyOutline();
        return this;
    }

    /// <summary>
    /// 외곽선 두께를 설정합니다 (0 ~ 1).
    /// </summary>
    public UI_Text SetOutlineThickness(float thickness)
    {
        Initialize();
        _outlineSettings.thickness = Mathf.Clamp01(thickness);
        ApplyOutline();
        return this;
    }

    /// <summary>
    /// 글자 확장/축소를 설정합니다 (-1 ~ 1).
    /// </summary>
    public UI_Text SetOutlineDilate(float dilate)
    {
        Initialize();
        _outlineSettings.dilate = Mathf.Clamp(dilate, -1f, 1f);
        ApplyOutline();
        return this;
    }

    /// <summary>
    /// 외곽선 설정을 한번에 적용합니다.
    /// </summary>
    public UI_Text SetOutline(OutlineSettings settings)
    {
        Initialize();
        _outlineSettings = settings;
        ApplyOutline();
        return this;
    }

    /// <summary>
    /// 외곽선 설정을 한번에 적용합니다.
    /// </summary>
    public UI_Text SetOutline(Color color, float thickness = 0f, float dilate = 0f)
    {
        Initialize();
        _outlineSettings = new OutlineSettings(color, thickness, dilate);
        ApplyOutline();
        return this;
    }

    #endregion

    #region Font Setting

    /// <summary>
    /// 폰트를 설정합니다.
    /// </summary>
    public UI_Text SetFont(TMP_FontAsset font)
    {
        Initialize();

        _txt.font = font;

        return this;
    }

    /// <summary>
    /// 폰트 머티리얼을 설정합니다.
    /// </summary>
    public UI_Text SetFontMaterial(Material material)
    {
        Initialize();

        _txt.fontMaterial = material;

        return this;
    }

    #endregion

    #region Localization

    /// <summary>
    /// 로컬라이제이션 키를 설정합니다.
    /// </summary>
    public UI_Text SetLocaleName(ELocalizedName localeName)
    {
        Initialize();
        _localeName = localeName;
        ApplyLocalization();
        return this;
    }

    /// <summary>
    /// 포맷 인자를 설정하고 로컬라이제이션을 다시 적용합니다.
    /// 예: "Level {0}" → SetFormatArgs(15) → "Level 15"
    /// </summary>
    public UI_Text SetFormatArgs(params object[] args)
    {
        Initialize();
        _formatArgs = args;
        ApplyLocalization();
        return this;
    }

    /// <summary>
    /// 로컬라이제이션을 수동으로 적용합니다.
    /// </summary>
    public async void ApplyLocalization()
    {
        if (_localeName == ELocalizedName.NONE) return;
        await UniTask.WaitUntil(() => Main.Local.IsInitialized);
        Initialize();

        string localizedText = Main.Local.GetLocalString(_localeName);

        // 포맷 인자 적용
        if (_formatArgs != null && _formatArgs.Length > 0)
        {
            try
            {
                localizedText = string.Format(localizedText, _formatArgs);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[UI_Text] Format failed for '{_localeName}': {e.Message}");
            }
        }

        SetTextInternal(localizedText);
    }

    // 언어 변경 이벤트 구독
    private async void SubscribeLanguageEvent()
    {
        if (_isLanguageEventSubscribed) return;
        if (_localeName == ELocalizedName.NONE) return;
        await UniTask.WaitUntil(() => Main.Local.IsInitialized);
        Initialize();

        Main.Local.OnLanguageChanged += OnLanguageChanged;
        _isLanguageEventSubscribed = true;
    }

    // 언어 변경 이벤트 구독 해제
    private async void UnsubscribeLanguageEvent()
    {
        if (!_isLanguageEventSubscribed) return;
        await UniTask.WaitUntil(() => Main.Local.IsInitialized);
        Initialize();

        Main.Local.OnLanguageChanged -= OnLanguageChanged;
        _isLanguageEventSubscribed = false;
    }

    // 언어 변경 시 호출
    private void OnLanguageChanged()
    {
        ApplyLocalization();
    }

    #endregion

    #region Internal Methods

    // 텍스트 설정 내부 로직
    private void SetTextInternal(string value)
    {
        Initialize();
        if (_autoResize) ResizeToFit(value);

        if (OnChangeText != null)
            OnChangeText.Invoke(value);
        else
            _txt.text = value;
    }

    // 외곽선 적용
    private void ApplyOutline()
    {
        Initialize();

        if (_outlineSettings.IsDefault(Color))
        {
            SetFontMaterial(_defaultMat);
        }
        else
        {
            Main.Text?.SetOutline(this);
        }
    }

    // 텍스트 크기에 맞게 부모 리사이즈
    private void ResizeToFit(string text)
    {
        if (text == _txt.text) return;
        if (_resizeParent == null) return;
        Initialize();

        Vector2 size = _txt.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity);
        RectTransform rect = _txt.rectTransform;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

        Vector2 sizeDelta = rect.sizeDelta;
        if (sizeDelta.x < sizeDelta.y) sizeDelta.x = sizeDelta.y;

        _txt.text = text;
        _resizeParent.sizeDelta = sizeDelta + (Vector2.one * _resizePadding);
    }

    #endregion

    #region Editor Preview

#if UNITY_EDITOR
    [Header("Editor Preview")]
    [SerializeField] private bool _previewOutline = true;

    // 에디터 전용 임시 머티리얼
    private Material _editorPreviewMat;

    private void OnValidate()
    {
        if (!TryGetComponent(out TMP_Text txt)) return;
        if (txt.font == null) return;

        // 로컬라이제이션 키 미리보기
        if (_localeName != ELocalizedName.NONE)
        {
            txt.text = $"[{_localeName}]";
        }

        // 외곽선 미리보기
        if (_previewOutline)
        {
            bool needsOutline = !_outlineSettings.IsDefault(txt.color);

            if (needsOutline)
            {
                if (_editorPreviewMat == null || _editorPreviewMat.shader != txt.font.material.shader)
                {
                    _editorPreviewMat = new Material(txt.font.material);
                }

                _editorPreviewMat.SetColor(ShaderUtilities.ID_OutlineColor, _outlineSettings.color);
                _editorPreviewMat.SetFloat(ShaderUtilities.ID_OutlineWidth, _outlineSettings.thickness);
                _editorPreviewMat.SetFloat(ShaderUtilities.ID_FaceDilate, _outlineSettings.dilate);

                txt.fontMaterial = _editorPreviewMat;
            }
            else
            {
                txt.fontMaterial = txt.font.material;
            }
        }

        txt.SetAllDirty();
        txt.ForceMeshUpdate();
    }
#endif

    #endregion
}

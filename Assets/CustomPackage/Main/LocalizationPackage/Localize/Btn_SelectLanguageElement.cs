using System;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class Btn_SelectLanguageElement : UI_Button
{
    [SerializeField] private LocalizedCountryType localizedCountryType;
    private UI_Image _imgSelectLanguageElement;
    private TextMeshProUGUI _textSelectLanguageElement;
    public static Action<LocalizedCountryType> OnLanguageSelected;
    public static Action OnClickSelectLanguage;
    
    private SettingPrefs _settingPrefs;

    public override bool Initialize()
    {
        if(!base.Initialize()) return false;
        _settingPrefs = Prefs.Get<SettingPrefs>();
        _imgSelectLanguageElement = gameObject.FindChild<UI_Image>("Img_SelectLanguageElement");
        _textSelectLanguageElement = gameObject.FindChild<TextMeshProUGUI>("Text_SelectLanguageElement");
        SelectLanguageElement((LocalizedCountryType)_settingPrefs.Language.Value);
        SetEvent(OnClickSelectLanguageElement);
        return true;
    }

    protected void OnEnable()
    {
        OnLanguageSelected += SelectLanguageElement;
    }

    protected void OnDisable()
    {
        OnLanguageSelected -= SelectLanguageElement;
    }

    private async void SelectLanguageElement(LocalizedCountryType countryType)
    {
        _imgSelectLanguageElement.gameObject.SetActive(countryType == localizedCountryType);
        if (countryType == localizedCountryType)
        {
            await UniTask.WaitUntil(() => SelectLanguage.OnLanguageChanged != null);
            SelectLanguage.OnLanguageChanged?.Invoke(_textSelectLanguageElement.text);
        }
    }
    
    private void OnClickSelectLanguageElement()
    {
        Initialize();
        OnLanguageSelected?.Invoke(localizedCountryType);
        OnClickSelectLanguage?.Invoke();
    }
}
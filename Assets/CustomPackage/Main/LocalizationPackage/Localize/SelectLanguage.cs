using System;
using UnityEngine;

public class SelectLanguage : MonoBehaviour
{
    private bool _isInitialized = false;
    
    private UI_Button _btnSelectLanguage;
    private UI_Text _textSelectLanguage;
    private UI_Image _panelSelectLanguage;
    private UI_Image _imgSelectLanguageArrow;
    
    private Btn_SelectLanguageElement[] _btnSelectLanguageElements;
    
    public static Action<string> OnLanguageChanged;

    private void OnEnable()
    {
        Initialized();
    }

    public void Initialized()
    {
        if (_isInitialized) return;
        _panelSelectLanguage = gameObject.FindChild<UI_Image>("Panel_SelectLanguage");
        _btnSelectLanguage = gameObject.FindChild<UI_Button>("Btn_SelectLanguage");
        _imgSelectLanguageArrow = gameObject.FindChild<UI_Image>("Img_Cursor");
        _textSelectLanguage = gameObject.FindChild<UI_Text>("Text_SelectLanguage");
        
        _btnSelectLanguageElements = gameObject.GetComponentsInChildren<Btn_SelectLanguageElement>(true);
        foreach (var btnSelectLanguageElement in _btnSelectLanguageElements) btnSelectLanguageElement.Initialize();
        
        OnLanguageChanged += SetSelectLanguageText;
        Btn_SelectLanguageElement.OnClickSelectLanguage += ToggleSelectLanguagePanel;
        _btnSelectLanguage.SetEvent(ToggleSelectLanguagePanel);
        _isInitialized = true;
    }

    private void OnDestroy()
    {
        Btn_SelectLanguageElement.OnClickSelectLanguage -= ToggleSelectLanguagePanel;
    }

    private void ToggleSelectLanguagePanel()
    {
        _panelSelectLanguage.gameObject.SetActive(!_panelSelectLanguage.gameObject.activeSelf);
        // float nowRotationX = _panelSelectLanguage.gameObject.activeSelf ? 180 : 0;
        // _imgSelectLanguageArrow.SetRotationX(nowRotationX);
    }

    private void SetSelectLanguageText(string text)
    {
        Initialized();
        _textSelectLanguage.Text = text;
    }
}
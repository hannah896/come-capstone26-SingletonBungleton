using System;
using System.Collections.Generic;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 텍스트들의 외곽선 및 포맷을 관리해주는 매니저
/// </summary>
public class TextManager : ContentManager
{
    private const string PlayerLevelKey = "PlayerLevel";
    private const string PlayerGoldKey = "PlayerGold";
    private const string PlayerHearthKey = "PlayerHearth";
    
    private FontsSo _fontsSo;
    public FontsSo FontSo => _fontsSo;
    
    private FontData _fontData;
    private Dictionary<TMP_FontAsset, FontData> _dictFontsData = new();
    private Dictionary<string, string> currentData = new();
    
    private PlayPrefs _playPrefs;
    private CurrencyPrefs _currencyPrefs;

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await InitFontSo();
        
        _currencyPrefs = Prefs.Get<CurrencyPrefs>();
        _playPrefs = Prefs.Get<PlayPrefs>();
        
        InitData();
    }

    private void InitData()
    {
        try
        {
            currentData[PlayerLevelKey] = (_playPrefs.Stage.Value).ToString();
            currentData[PlayerGoldKey] = (_currencyPrefs.Currency.Value).ToString();
            currentData[PlayerHearthKey] = (_currencyPrefs.Lives.Value).ToString();
        
            _playPrefs.Stage.OnValueChanged += i => currentData[PlayerLevelKey] = i.ToString();
            _currencyPrefs.Currency.OnValueChanged += i => currentData[PlayerGoldKey] = i.ToString();
            _currencyPrefs.Lives.OnValueChanged += i => currentData[PlayerHearthKey] = i.ToString();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private async UniTask InitFontSo()
    {
        try
        {
            Main.Local.OnSettingFont += SetFontData;
            _fontsSo = await Main.Resource.LoadAssetAsync<FontsSo>("FontsSo");
            InitFontData();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void InitFontData()
    {
        foreach (TMP_FontAsset item in _fontsSo.dictFontsData.Values)
        {
            _dictFontsData[item] = new FontData(item);
        }
    }

    private void SetFontData()
    {
        if (!_fontsSo.dictFontsData.TryGetValue(_playPrefs.LocalizeCountry, out TMP_FontAsset fontAsset))
        {
            Debug.LogError($"Failed Search Font Data: {_playPrefs.LocalizeCountry}");
            return;
        }
        _fontData = new FontData(fontAsset);
    }

    public void SetFont(UI_Text text)
    {
        Color textColor = text.TMPText.color;
        TMP_FontAsset font = _fontData.originalFont;
        if (!_dictFontsData.TryGetValue(font, out var fontData))
        {
            Debug.LogError($"not found fontData : {text.TMPText.font}");
            return;
        }
        Material fontMat = fontData.GetMaterial(text.outLineColor);

        text.SetFont(font);
        text.SetFontMaterial(fontMat);
    }

    public void SetOutlineColor(UI_Text text)
    {
        TMP_FontAsset font = text.TMPText.font;
        if (!_dictFontsData.TryGetValue(font, out var fontData))
        {
            fontData = new FontData(font);
            _dictFontsData[font] = fontData;
        }
        Material fontMat = fontData.GetMaterial(text.outLineColor);

        text.SetFontMaterial(fontMat);
    }
}
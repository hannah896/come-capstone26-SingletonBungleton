using System;
using System.Collections.Generic;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 텍스트들의 외곽선 및 포맷을 관리해주는 매니저.
/// 로컬라이제이션에 따른 폰트 설정과 외곽선 색상을 처리합니다.
/// </summary>
public class TextManager : CoreManager
{
    #region Constants

    private const string PlayerGoldKey = "PlayerGold";
    private const string PlayerHearthKey = "PlayerHearth";

    #endregion

    #region Fields

    // 폰트 ScriptableObject
    private FontsSo _fontsSo;

    // 현재 폰트 데이터
    private FontData _fontData;

    // 폰트별 데이터 캐시
    private Dictionary<TMP_FontAsset, FontData> _dictFontsData = new();

    // 플레이어 데이터 캐시
    private Dictionary<string, string> currentData = new();

    // 플레이 프리퍼런스
    private PlayPrefs _playPrefs;

    // 재화 프리퍼런스
    private CurrencyPrefs _currencyPrefs;

    #endregion

    #region Properties

    public FontsSo FontSo => _fontsSo;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await InitFontSo();

        _currencyPrefs = Prefs.Get<CurrencyPrefs>();
        _playPrefs = Prefs.Get<PlayPrefs>();

        InitData();
    }

    // 데이터 초기화
    private void InitData()
    {
        try
        {
            currentData[PlayerGoldKey] = (_currencyPrefs.Currency.Value).ToString();
            currentData[PlayerHearthKey] = (_currencyPrefs.Lives.Value).ToString();

            _currencyPrefs.Currency.OnValueChanged += i => currentData[PlayerGoldKey] = i.ToString();
            _currencyPrefs.Lives.OnValueChanged += i => currentData[PlayerHearthKey] = i.ToString();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 폰트 ScriptableObject 초기화
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

    // 폰트 데이터 초기화
    private void InitFontData()
    {
        foreach (var (key, item) in _fontsSo.dictFontsData)
        {
            if (item == null)
            {
                Debug.LogError($"[TextManager] FontsSo.dictFontsData에 null인 TMP_FontAsset가 있습니다. (Key: {key})");
                continue;
            }
            _dictFontsData[item] = new FontData(item);
        }
    }

    // 현재 언어에 맞는 폰트 데이터 설정
    private void SetFontData()
    {
        if (!_fontsSo.dictFontsData.TryGetValue(_playPrefs.LocalizeCountry, out TMP_FontAsset fontAsset))
        {
            Debug.LogError($"Failed Search Font Data: {_playPrefs.LocalizeCountry}");
            return;
        }
        _fontData = new FontData(fontAsset);
    }

    #endregion

    #region Font

    /// <summary>
    /// UI 텍스트에 폰트를 설정합니다.
    /// </summary>
    public void SetFont(UI_Text text)
    {
        TMP_FontAsset font = _fontData.originalFont;
        if (!_dictFontsData.TryGetValue(font, out var fontData))
        {
            Debug.LogError($"not found fontData : {text.TMP.font}");
            return;
        }
        Material fontMat = fontData.GetMaterial(text.OutlineSettings);

        text.SetFont(font);
        text.SetFontMaterial(fontMat);
    }

    /// <summary>
    /// UI 텍스트의 외곽선을 설정합니다.
    /// </summary>
    public void SetOutline(UI_Text text)
    {
        TMP_FontAsset font = text.TMP.font;
        if (!_dictFontsData.TryGetValue(font, out var fontData))
        {
            fontData = new FontData(font);
            _dictFontsData[font] = fontData;
        }
        Material fontMat = fontData.GetMaterial(text.OutlineSettings);

        text.SetFontMaterial(fontMat);
    }

    /// <summary>
    /// UI 텍스트의 외곽선 색상을 설정합니다 (기존 호환용).
    /// </summary>
    public void SetOutlineColor(UI_Text text)
    {
        SetOutline(text);
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// 전체 캐시된 머티리얼 개수를 반환합니다.
    /// </summary>
    public int GetTotalCachedCount()
    {
        int total = 0;
        foreach (var fontData in _dictFontsData.Values)
        {
            total += fontData.CachedCount;
        }
        return total;
    }

    /// <summary>
    /// 캐시된 폰트 개수를 반환합니다.
    /// </summary>
    public int GetCachedFontCount() => _dictFontsData.Count;

    /// <summary>
    /// 특정 폰트의 캐시된 머티리얼 개수를 반환합니다.
    /// </summary>
    public int GetCachedCount(TMP_FontAsset font)
    {
        if (_dictFontsData.TryGetValue(font, out var fontData))
        {
            return fontData.CachedCount;
        }
        return 0;
    }

    /// <summary>
    /// 모든 머티리얼 캐시를 초기화합니다.
    /// </summary>
    public void ClearAllCache()
    {
        foreach (var fontData in _dictFontsData.Values)
        {
            fontData.ClearCache();
        }
        Debug.Log("[TextManager] All material cache cleared.");
    }

    /// <summary>
    /// 특정 폰트의 머티리얼 캐시를 초기화합니다.
    /// </summary>
    public void ClearCache(TMP_FontAsset font)
    {
        if (_dictFontsData.TryGetValue(font, out var fontData))
        {
            fontData.ClearCache();
            Debug.Log($"[TextManager] Cache cleared for font: {font.name}");
        }
    }

    /// <summary>
    /// 캐시 상태를 콘솔에 출력합니다 (디버그용).
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void DebugPrintCacheStatus()
    {
        Debug.Log("========== TextManager Cache Status ==========");
        Debug.Log($"Total Fonts: {_dictFontsData.Count}");
        Debug.Log($"Total Cached Materials: {GetTotalCachedCount()}");
        Debug.Log("-----------------------------------------------");

        foreach (var kvp in _dictFontsData)
        {
            var font = kvp.Key;
            var fontData = kvp.Value;
            Debug.Log($"  [{font.name}] Materials: {fontData.CachedCount}");

            foreach (var settings in fontData.GetCachedSettings())
            {
                Debug.Log($"    - Color: {settings.color}, Thickness: {settings.thickness}, Dilate: {settings.dilate}");
            }
        }
        Debug.Log("===============================================");
    }

    #endregion
}

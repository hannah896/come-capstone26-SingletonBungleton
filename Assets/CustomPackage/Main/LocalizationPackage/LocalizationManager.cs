using System;
using System.Collections.Generic;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 로컬라이징을 관리해주는 매니저.
/// Unity Localization패키지를 필요로 함.
/// 해당 Localization테이블에 등록되어있는 키값을 기준으로 UI_Text의 Text들을 바꿔줌.
/// 해당 키값은 UI_Text.cs에서 등록해주면 됨.
/// </summary>
public class LocalizationManager : CoreManager
{
    private const string TableRef = "base";
        
    private Locale CurrentLanguage => LocalizationSettings.SelectedLocale;
        
    public event Action OnLanguageChanged;
    public event Action OnSettingFont;
    private readonly HashSet<int> _validEnumValues = 
        new HashSet<int>((int[])Enum.GetValues(typeof(ELocalizedName)));
    
    private SettingPrefs _settingPrefs;

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        _settingPrefs = Prefs.Get<SettingPrefs>();
        Btn_SelectLanguageElement.OnLanguageSelected += ChangeLocale;
    }

    #region Localization Management
    
    // 해당 언어로 텍스트들을 변경하는 함수
    public async void ChangeLocale(LocalizedCountryType localizedCountryType = LocalizedCountryType.None)
    {
        Initialize();
        LocalizedCountryType inputCountryType = localizedCountryType;
        if(inputCountryType != LocalizedCountryType.None) Main.Loading.Show(LoadingType.Transition);
        if (localizedCountryType == LocalizedCountryType.None) localizedCountryType = (LocalizedCountryType)_settingPrefs.Language.Value;
        AsyncOperationHandle<LocalizationSettings> initOp = LocalizationSettings.InitializationOperation;
        await UniTask.WaitUntil(() => initOp.IsDone);
        
        _settingPrefs.Language.Value = (int)localizedCountryType;
        
        var locale = LocalizationSettings.AvailableLocales.GetLocale(localizedCountryType.ToString());
        LocalizationSettings.SelectedLocale = locale;
        
        var tableLoadOp = LocalizationSettings.StringDatabase.GetTableAsync(TableRef);
        await UniTask.WaitUntil(() => tableLoadOp.IsDone);
        
        if (tableLoadOp.Status != AsyncOperationStatus.Succeeded)
            Debug.LogError($"[Localization] Table '{TableRef}' load status: {tableLoadOp.Status}");
        
        //테이블이 로드될 때까지 대기
        await UniTask.NextFrame();
        OnSettingFont?.Invoke();
        OnLanguageChanged?.Invoke();
        await UniTask.WaitForSeconds(1);
        if(inputCountryType != LocalizedCountryType.None) Main.Loading.Hide();
    }
        
    #endregion
        
    #region String Retrieval
        
    /// <summary>
    /// 키값에 해당하는 로컬 번역 문자열을 반환하는 메서드
    /// </summary>
    public string GetLocalString(string localKey)
    {
        if (string.IsNullOrEmpty(localKey)) return string.Empty;
        string word = LocalizationSettings.StringDatabase.GetLocalizedString(TableRef, localKey, CurrentLanguage);
        if (string.IsNullOrEmpty(word)) return $"Localize {localKey} not found";
        return word.Contains(@"\\") ? word.Replace(@"\\", "\n") : word;
    }

    public string GetLocalString(ELocalizedName localizedName, string originalText = "[Localize failed]")
    {
        int num = (int)localizedName;
        if (!_validEnumValues.Contains(num)) return originalText;
        return GetLocalString(localizedName.ToString());
    }
        
    public string GetFormattedLocalString(string key, params object[] args)
    {
        string localizedString = GetLocalString(key);
        return string.Format(localizedString, args);
    }
        
    #endregion
}

public enum LocalizedCountryType
{
    None = 0,
    en = 1,
    ja = 2,
    ko = 3,
}
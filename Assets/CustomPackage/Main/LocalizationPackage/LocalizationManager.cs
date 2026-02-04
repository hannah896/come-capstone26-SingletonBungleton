using System;
using System.Collections.Generic;
using Blossom.Preference;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

#region Enums

/// <summary>
/// 로컬라이즈 지원 언어 타입.
/// </summary>
public enum LocalizedCountryType
{
    None = 0,
    en = 1,
    ja = 2,
    ko = 3,
}

#endregion

/// <summary>
/// 로컬라이징을 관리해주는 매니저.
/// Unity Localization 패키지를 필요로 합니다.
/// Localization 테이블에 등록되어있는 키값을 기준으로 UI_Text의 Text들을 바꿔줍니다.
/// </summary>
public class LocalizationManager : CoreManager
{
    #region Constants

    private const string TableRef = "base";

    #endregion

    #region Fields

    // 유효한 enum 값 캐시
    private readonly HashSet<int> _validEnumValues =
        new HashSet<int>((int[])Enum.GetValues(typeof(ELocalizedName)));

    // 설정 프리퍼런스
    private SettingPrefs _settingPrefs;

    #endregion

    #region Properties

    // 현재 선택된 언어
    private Locale CurrentLanguage => LocalizationSettings.SelectedLocale;

    #endregion

    #region Events

    // 언어 변경 시 발생
    public event Action OnLanguageChanged;

    // 폰트 설정 시 발생
    public event Action OnSettingFont;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        _settingPrefs = Prefs.Get<SettingPrefs>();
        Btn_SelectLanguageElement.OnLanguageSelected += ChangeLocale;
    }

    #endregion

    #region Localization Management

    /// <summary>
    /// 해당 언어로 텍스트들을 변경합니다.
    /// </summary>
    public async void ChangeLocale(LocalizedCountryType localizedCountryType = LocalizedCountryType.None)
    {
        LocalizedCountryType inputCountryType = localizedCountryType;
        if (inputCountryType != LocalizedCountryType.None) Main.UI.ShowScreenAsync<UI_Screen_Fade>();
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

        // 테이블이 로드될 때까지 대기
        await UniTask.NextFrame();
        OnSettingFont?.Invoke();
        OnLanguageChanged?.Invoke();
        await UniTask.WaitForSeconds(1);
        if (inputCountryType != LocalizedCountryType.None) Main.UI.HideScreenAsync();
    }

    #endregion

    #region String Retrieval

    /// <summary>
    /// 키값에 해당하는 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public string GetLocalString(string localKey)
    {
        if (string.IsNullOrEmpty(localKey)) return string.Empty;
        string word = LocalizationSettings.StringDatabase.GetLocalizedString(TableRef, localKey, CurrentLanguage);
        if (string.IsNullOrEmpty(word)) return $"Localize {localKey} not found";
        return word.Contains(@"\\") ? word.Replace(@"\\", "\n") : word;
    }

    /// <summary>
    /// ELocalizedName enum으로 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public string GetLocalString(ELocalizedName localizedName, string originalText = "[Localize failed]")
    {
        int num = (int)localizedName;
        if (!_validEnumValues.Contains(num)) return originalText;
        return GetLocalString(localizedName.ToString());
    }

    /// <summary>
    /// 포맷이 적용된 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public string GetFormattedLocalString(string key, params object[] args)
    {
        string localizedString = GetLocalString(key);
        return string.Format(localizedString, args);
    }

    #endregion
}

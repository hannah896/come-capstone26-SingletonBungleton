using Blossom.Preference;
using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;

/// <summary>
/// 오디오들의 출력들을 관리해주는 매니저.
/// JSAM(Simple Audio Manager)을 래핑하여 BGM/SFX를 제어합니다.
/// </summary>
public class JSAMManager : PrimaryManager
{
    #region Fields

    // BGM 설정 여부
    private bool _setBGM;

    // SFX 설정 여부
    private bool _setSFX;

    // 설정 프리퍼런스
    private SettingPrefs _settingPrefs;

    #endregion

    #region Properties

    // BGM 활성화 여부
    public bool SetBGM => _settingPrefs.BGM.Value;

    // SFX 활성화 여부
    public bool SetSFX => _settingPrefs.SFX.Value;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        // 씬이 완전히 로드될 때까지 대기 (BeforeSceneLoad에서 호출되므로)
        await UniTask.Yield(PlayerLoopTiming.Update);

        // 오디오 매니저 오브젝트 탐색/생성
        if (!Object.FindFirstObjectByType<AudioManager>())
        {
            AudioManager go = await Main.Resource.LoadAssetAsync<AudioManager>("AudioManager");
            if (go != null)
            {
                var prefab = GameObject.Instantiate(go);
                prefab.name = "@Audio";
                prefab.transform.SetSiblingIndex(2);
            }
        }

        _settingPrefs = Prefs.Get<SettingPrefs>();
        _settingPrefs.BGM.OnValueChanged += SetMusicPlay;
        _settingPrefs.SFX.OnValueChanged += SetSoundPlay;
    }

    #endregion

    #region Volume

    // 볼륨 영속성(PlayerPrefs 저장/로드)은 JSAM이 자동 처리한다.
    // - AudioManager.Awake()에서 LoadVolumeSettings()로 자동 로드 (게임 시작 시 자동 적용)
    // - AudioManager.OnDestroy()에서 SaveVolumeSettings()로 자동 저장
    // 따라서 여기서는 AudioManager의 채널 볼륨만 set/get한다.

    // 마스터 볼륨 (0~1)
    public float MasterVolume => AudioManager.Instance != null ? AudioManager.MasterVolume : 1f;

    // BGM(음악) 볼륨 (0~1)
    public float BGMVolume => AudioManager.Instance != null ? AudioManager.MusicVolume : 1f;

    // SFX(효과음) 볼륨 (0~1)
    public float SFXVolume => AudioManager.Instance != null ? AudioManager.SoundVolume : 1f;

    /// <summary>
    /// 마스터 볼륨을 설정합니다. (0~1)
    /// </summary>
    public void SetMasterVolume(float value)
    {
        if (AudioManager.Instance == null) return;
        AudioManager.MasterVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// BGM(음악) 볼륨을 설정합니다. (0~1)
    /// </summary>
    public void SetBGMVolume(float value)
    {
        if (AudioManager.Instance == null) return;
        AudioManager.MusicVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// SFX(효과음) 볼륨을 설정합니다. (0~1)
    /// </summary>
    public void SetSFXVolume(float value)
    {
        if (AudioManager.Instance == null) return;
        AudioManager.SoundVolume = Mathf.Clamp01(value);
    }

    /// <summary>
    /// 현재 볼륨 설정을 PlayerPrefs에 즉시 저장합니다.
    /// (JSAM이 종료 시 자동 저장도 하지만, 명시적으로 저장하고 싶을 때 호출)
    /// </summary>
    public void SaveVolume()
    {
        AudioManager.InternalInstance?.SaveVolumeSettings();
    }

    #endregion

    #region Audio Control

    /// <summary>
    /// 음악 재생 설정을 변경합니다.
    /// </summary>
    public void SetMusicPlay(bool active) => AudioManager.MusicMuted = !active;

    /// <summary>
    /// 효과음 재생 설정을 변경합니다.
    /// </summary>
    public void SetSoundPlay(bool active) => AudioManager.SoundMuted = !active;

    /// <summary>
    /// BGM을 재생합니다.
    /// </summary>
    public void PlayBGM(AudioLibraryMusic clip, float vol = 0.8f)
    {
        if (!SetBGM) return;
        AudioManager.StopAllMusic();
        // 채널 볼륨(MusicVolume)은 설정(UI_Popup_SettingUI)에서 관리하므로 여기서 덮어쓰지 않는다.
        AudioManager.PlayMusic(clip);
#if UNITY_EDITOR
        Debug.Log($"PlayBGM: {clip}");
#endif
    }

    /// <summary>
    /// SFX를 재생합니다.
    /// </summary>
    public void PlaySFX(AudioLibrarySounds clip, float vol = 0.8f)
    {
        if (!SetSFX) return;
        // 채널 볼륨(SoundVolume)은 설정(UI_Popup_SettingUI)에서 관리하므로 여기서 덮어쓰지 않는다.
        AudioManager.PlaySound(clip);
#if UNITY_EDITOR
        Debug.Log($"PlaySFX: {clip}");
#endif
    }

    /// <summary>
    /// 랜덤 SFX를 재생합니다.
    /// </summary>
    public void PlayRandomSFX(AudioLibrarySounds[] clip, float vol = 0.8f)
    {
        if (!SetSFX) return;
        int randomIndex = Random.Range(0, clip.Length);
        PlaySFX(clip[randomIndex], vol);
    }

    /// <summary>
    /// BGM을 정지합니다.
    /// </summary>
    public void StopBGM()
    {
        AudioManager.StopAllMusic();
    }

    /// <summary>
    /// SFX를 정지합니다.
    /// </summary>
    public void StopSFX()
    {
        AudioManager.StopAllSounds();
    }

    /// <summary>
    /// 모든 사운드를 정지합니다.
    /// </summary>
    public void StopAllSound()
    {
        StopBGM();
        StopSFX();
    }

    #endregion
}

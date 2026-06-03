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
        AudioManager.MusicVolume = vol;
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
        AudioManager.SoundVolume = vol;
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

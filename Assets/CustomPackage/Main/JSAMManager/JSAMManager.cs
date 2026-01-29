using Blossom.Preference;
using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;

/// <summary>
/// 오디오들의 출력들을 관리해주는 매니저.
/// </summary>
public class JSAMManager : ContentManager
{
    #region Field
    
    private bool _setBGM;
    private bool _setSFX;
    private SettingPrefs _settingPrefs;

    #endregion

    #region Property

    public bool SetBGM => _settingPrefs.BGM.Value;
    public bool SetSFX => _settingPrefs.SFX.Value;

    #endregion

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        // 오디오 매니저 오브젝트 탐색
        if (!Object.FindFirstObjectByType<AudioManager>())
        {
            AudioManager go = await Main.Resource.LoadAssetAsync<AudioManager>("AudioManager");
            go = new GameObject("[AudioManager]").AddComponent<AudioManager>();
        }
        
        _settingPrefs = Prefs.Get<SettingPrefs>();
        _settingPrefs.BGM.OnValueChanged += SetMusicPlay; 
        _settingPrefs.SFX.OnValueChanged += SetSoundPlay; 
    }

    #region Audio

    public void SetMusicPlay(bool active) => AudioManager.MusicMuted = !active;
    public void SetSoundPlay(bool active) => AudioManager.SoundMuted = !active;

    // BGM 플레이
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

    // SFX 플레이
    public void PlaySFX(AudioLibrarySounds clip, float vol = 0.8f)
    {
        if (!SetSFX) return;
        AudioManager.SoundVolume = vol;
        AudioManager.PlaySound(clip);
#if UNITY_EDITOR
        Debug.Log($"PlaySFX: {clip}");
#endif
    }

    // SFX 랜덤 플레이
    public void PlayRandomSFX(AudioLibrarySounds[] clip, float vol = 0.8f)
    {
        if (!SetSFX) return;
        int randomIndex = Random.Range(0, clip.Length);
        PlaySFX(clip[randomIndex], vol);
        Main.JSAM.PlaySFX(clip[randomIndex], vol);
    }

    public void StopBGM()
    {
        AudioManager.StopAllMusic();
    }

    public void StopSFX()
    {
        AudioManager.StopAllSounds();
    }

    public void StopAllSound()
    {
        StopBGM();
        StopSFX();
    }

    #endregion
}
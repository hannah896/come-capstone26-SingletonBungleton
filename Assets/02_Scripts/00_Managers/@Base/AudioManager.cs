using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;


public class AudioManager
{
    private SO_AudioOption _audioOption;
    private AudioMixer _audioMixer;

    private const string MIXER_BGM_PARAM = "BGM_Volume";
    private const string MIXER_SFX_PARAM = "SFX_Volume";

    private AudioSource _bgmSource;
    private AudioSource _oneShotSource;
    private readonly List<AudioSource> _sfxPool = new();

    private GameObject _root;

    public async UniTask Init(Transform parent = null)
    {
        _root = new GameObject("[SoundManager]");
        if (parent != null) _root.transform.SetParent(parent);
        else Object.DontDestroyOnLoad(_root);

        _audioOption = await Managers.Resource.LoadAssetAsync<SO_AudioOption>("SO_AudioOption", AssetCacheType.Required);
        _audioMixer = await Managers.Resource.LoadAssetAsync<AudioMixer>("AudioMixer");

        _bgmSource = CreateSource("BGM_Source", "BGM", true);
        _oneShotSource = CreateSource("OneShot_Source", "SFX", false);

        ApplyAllVolumes();
    }

    private AudioSource CreateSource(string name, string groupName, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_root.transform);
        var source = go.AddComponent<AudioSource>();
        source.loop = loop;
        source.playOnAwake = false;

        var groups = _audioMixer.FindMatchingGroups(groupName);
        if (groups.Length > 0) source.outputAudioMixerGroup = groups[0];

        return source;
    }

    public async UniTask PlayBgmAsync(string key, CancellationToken ct = default)
    {
        var clip = await Managers.Resource.LoadAssetAsync<AudioClip>(key, cancellationToken: ct);
        if (clip == null || _bgmSource.clip == clip) return;

        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public async UniTask PlaySfxAsync(string key, bool usePool = false, float pitch = 1f, CancellationToken ct = default)
    {
        var clip = await Managers.Resource.LoadAssetAsync<AudioClip>(key, cancellationToken: ct);
        if (clip == null) return;

        if (!usePool)
        {
            _oneShotSource.pitch = pitch;
            _oneShotSource.PlayOneShot(clip);
        }
        else
        {
            var source = GetAvailablePoolSource();
            source.clip = clip;
            source.pitch = pitch;
            source.Play();
        }
    }

    private AudioSource GetAvailablePoolSource()
    {
        foreach (var s in _sfxPool)
            if (!s.isPlaying) return s;

        var newSource = CreateSource($"SFX_Pool_{_sfxPool.Count}", "SFX", false);
        _sfxPool.Add(newSource);
        return newSource;
    }

    public void ApplyAllVolumes()
    {
        if (_audioMixer == null || _audioOption == null) return;

        float bgmDB = Mathf.Log10(Mathf.Max(_audioOption.MasterVolume * _audioOption.BGMVolume, 0.0001f)) * 20f;
        float sfxDB = Mathf.Log10(Mathf.Max(_audioOption.MasterVolume * _audioOption.SFXVolume, 0.0001f)) * 20f;

        _audioMixer.SetFloat(MIXER_BGM_PARAM, bgmDB);
        _audioMixer.SetFloat(MIXER_SFX_PARAM, sfxDB);
    }

    public void ClearBGM() => _bgmSource?.Stop();
    public void ClearSFX(bool destroy = true)
    {
        _oneShotSource?.Stop();

        foreach (var s in _sfxPool)
        {
            if (s == null) continue;

            s.Stop();
            if (destroy)
                Object.Destroy(s.gameObject);
        }

        if (destroy)
            _sfxPool.Clear();
    }

    public void ClearAll(bool destroy = false)
    {
        ClearBGM();
        ClearSFX(destroy);
    }
}
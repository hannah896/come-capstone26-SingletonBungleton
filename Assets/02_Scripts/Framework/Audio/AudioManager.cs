using UnityEngine;
using UnityEngine.Audio;
using static Enums;

public class AudioManager
{
    private VolumeData data;

    public AudioController Controller = new();
    public AudioMixer Mixer;

    public VolumeData Data { get => data; }

    /// <summary>
    /// 오디오 매니저 초기화 메서드
    /// </summary>
    public void Init(AudioMixer mixer, VolumeData data)
    {
        Mixer = mixer;
        this.data = data;
    }

    public void SettingData()
    {
        SetVolume(SoundType.Master, data.ALL);
        SetVolume(SoundType.BGM, data.BGM);
        SetVolume(SoundType.SFX, data.SFX);
    }

    public void SetVolume(SoundType type, float value)
    {
        float dB = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
        switch (type)
        {
            case SoundType.Master:
                Mixer.SetFloat("Master", dB);
                data.ALL = value;
                break;
            case SoundType.BGM:
                Mixer.SetFloat("BGM", dB);
                data.BGM = value;
                break;
            case SoundType.SFX:
                Mixer.SetFloat("SFX", dB);
                data.SFX = value;
                break;
        }
    }
}
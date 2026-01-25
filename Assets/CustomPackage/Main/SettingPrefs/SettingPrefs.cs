using System;
using System.Runtime.Serialization;
using Blossom.Preference;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class SettingPrefs : PrefData
{
    #region Properties
    public PrefValue<bool> BGM => _bgm ??= new(true, this);
    public PrefValue<bool> SFX => _sfx ??= new(true, this);
    public PrefValue<bool> Haptic => _haptic ??= new(true, this);
    public PrefValue<int> Language => _language ??= new PrefValue<int>(0, this);
    #endregion

    #region Fields
    [SerializeField]
    private PrefValue<bool> _bgm;
    
    [SerializeField]
    private PrefValue<bool> _sfx;
    
    [SerializeField]
    private PrefValue<bool> _haptic;

    [SerializeField]
    private PrefValue<int> _language;
    #endregion

    public SettingPrefs() : base() { Clear(); }

    public override void Clear()
    {
        base.Clear();
        _haptic = new(true, this);
        _sfx = new(true, this);
        _bgm = new(true, this);
        _language = new(0, this);
    }
}
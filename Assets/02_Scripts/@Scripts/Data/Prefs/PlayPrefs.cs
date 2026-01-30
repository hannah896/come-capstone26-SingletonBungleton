using System;
using System.Runtime.Serialization;
using Blossom.Preference;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class PlayPrefs : PrefData {

    #region Properties
    
    public bool IsAcceptedRating
    {
        get => _isAcceptedRating;
        set
        {
            _isAcceptedRating = value;
            Prefs.OnChanged(Key);
        }
    }
    public string ProfileName {
        get => _profileName;
        set {
            if (_profileName == value) return;
            _profileName = value;
            OnProfileNameChanged?.Invoke(_profileName);
            Prefs.OnChanged(Key);
        }
    }

    public long FirstSessionTime {
        get => _firstSessionTime;
        set {
            _firstSessionTime = value;
            Prefs.OnChanged(Key);
        }
    }

    public long LastSessionTime {
        get => _lastSessionTime;
        set {
            _lastSessionTime = value;
            Prefs.OnChanged(Key);
        }
    }

    public LocalizedCountryType LocalizeCountry
    {
        get => (LocalizedCountryType)_localizeContry;
        set => _localizeContry = (int)value;
    }

    public PrefValue<int> Stage => _stage ??= new(1, this);
    
    #endregion
    
    #region Fields

    [SerializeField] private string _profileName;
    [SerializeField] private long _firstSessionTime;
    [SerializeField] private long _lastSessionTime;
    [SerializeField] private bool _isAcceptedRating;
    [SerializeField] private int _localizeContry;

    [SerializeField] private PrefValue<int> _stage;

    public event Action<string> OnProfileNameChanged;
    
    #endregion

    public PlayPrefs() : base() {
        Clear();
    }
    
    public override void Clear() {
        base.Clear();

        _profileName = "pLaYeR";
        _firstSessionTime = Def.TimeMin;
        _lastSessionTime = Def.TimeMin;
        _stage = new(1, this);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) {
        _stage ??= new(1, this);
    }
    
}
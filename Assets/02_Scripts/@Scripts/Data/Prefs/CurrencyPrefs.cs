using System;
using System.Runtime.Serialization;
using Blossom.Preference;
using UnityEngine;

[Serializable]
public class CurrencyPrefs : PrefData {

    #region Const.

    public const int InitialLives = 5;
    public const int MaxLives = 5;

    #endregion
    
    #region Properties

    public long LatestExitTime {
        get => _latestExitTime;
        set {
            if (_latestExitTime == value) return;
            _latestExitTime = value;
            Prefs.OnChanged(Key);
        }
    }

    public long LivesUseTime {
        get => _livesUseTime;
        set {
            if (_livesUseTime == value) return;
            _livesUseTime = value;
            Prefs.OnChanged(Key);
        }
    }

    public PrefValue<long> LivesUnlimitedRemainTime => _livesUnlimitedRemainTime ??= new(Def.TimeMin, this);
    public PrefValue<int> Lives => _lives ??= new(InitialLives, this);
    public PrefValue<int> Currency => _currency ??= new(400, this);
    public PrefValue<int> Item1Count => _item1Count ??= new(3, this);
    public PrefValue<int> Item2Count => _item2Count ??= new(3, this);
    public PrefValue<int> Item3Count => _item3Count ??= new(3, this);
    public PrefValue<int> Item4Count => _item4Count ??= new(3, this);

    #endregion

    #region Fields
    
    [SerializeField] private PrefValue<int> _currency;

    [SerializeField] private long _latestExitTime;
    [SerializeField] private long _livesUseTime;
    [SerializeField] private PrefValue<long> _livesUnlimitedRemainTime;
    [SerializeField] private PrefValue<int> _lives;
    [SerializeField] private PrefValue<int> _item1Count;
    [SerializeField] private PrefValue<int> _item2Count;
    [SerializeField] private PrefValue<int> _item3Count;
    [SerializeField] private PrefValue<int> _item4Count;

    #endregion

    public CurrencyPrefs() : base() {
        Clear();
    }

    public override void Clear() {
        base.Clear();

        _currency = new(400, this);

        _latestExitTime = Def.TimeMin;
        _livesUseTime = Def.TimeMin;
        _livesUnlimitedRemainTime = new(Def.TimeMin, this);
        _lives = new(InitialLives, this);
        _item1Count = new(3, this);
        _item2Count = new(3, this);
        _item3Count = new(3, this);
        _item4Count = new(3, this);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) {
        _currency ??= new(400, this);
        _livesUnlimitedRemainTime ??= new(Def.TimeMin, this);
        _lives ??= new(InitialLives, this);
    }

}

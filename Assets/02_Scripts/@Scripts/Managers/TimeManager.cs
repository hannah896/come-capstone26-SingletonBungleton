using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 실제 시간(UTC)을 기준으로 지난시간, 남은시간 등을 계산해주는 매니저.
/// </summary>
public class TimeManager : ContentManager {
    
    #region Properties

    public bool Pause { get; set; }
    
    #endregion
    
    #region Fields
    
    private Dictionary<string, NyoTimer> _timers = new();

    #endregion

    #region Initialize / Indexer

    public NyoTimer this[string key] => _timers.GetValueOrDefault(key);

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        GameEvents.OnGamePause += () => Pause = true;
        GameEvents.OnGameResume += () => Pause = false;
    }

    public override void Clear() {
        // TODO:: NyoTimer 실제 제거해야 함.
        foreach (NyoTimer timer in _timers.Values) timer.Clear();
        _timers.Clear();
        Pause = false;
    }
    
    #endregion

    public NyoTimer NewTimer(string key, float time, bool isAutoDestroy = false, bool start = true, bool loop = false) {
        if (_timers.TryGetValue(key, out NyoTimer timer)) timer.Clear();
        NyoTimer newTimer = new NyoTimer(key, time, isAutoDestroy, start, loop);
        _timers[key] = newTimer;
        newTimer.OnDestroy += t => {
            _timers.Remove(key);
        };
        return newTimer;
    }

    public void AddAllTime(float time, string[] excludes = null) {
        IEnumerable<NyoTimer> timers =
            excludes == null ? _timers.Values : _timers.Values.Where(t => !excludes.Contains(t.Key));
        foreach (NyoTimer timer in timers) timer.Add(time);
    }
    
    public void OnUpdate(float deltaTime) {
        if (Pause) return;
        if (GameScene.GameState != GameState.Playing) return;

        List<NyoTimer> timers = new(_timers.Values);
        foreach (NyoTimer timer in timers) {
            timer.OnUpdate(deltaTime);
        }
    }

}

public class NyoTimer {

    public string Key { get; private set; }

    public float Time { get; private set; }
    public float Current {
        get => _current;
        set {
            _current = value;
            OnChangedTime?.Invoke(this);

            if (_current <= 0) {
                OnTimeEnd?.Invoke(this);
                if (_loop) Current = Time;
                else if (_isAutoDestroy) OnDestroy?.Invoke(this);
            }
        }
    }
    public bool Pause { get; set; }

    private float _current;
    private bool _loop;             // 반복 여부.
    private bool _isAutoDestroy;             // 
    private float _secondOffset;    // 초 단위까지 남은 시간.

    public event Action<NyoTimer> OnChangedTime;
    public event Action<NyoTimer> OnSecond;
    public event Action<NyoTimer> OnTimeEnd;
    public event Action<NyoTimer> OnDestroy;

    public NyoTimer(string key, float time, bool isAutoDestroy = false, bool start = true, bool loop = false) {
        Key = key;
        Time = time;
        Pause = !start;
        _loop = loop;
        _isAutoDestroy = isAutoDestroy;
        
        _current = Time;
        _secondOffset = Time % 1;
    }

    public void Add(float time) {
        Current += time;
        OnSecond?.Invoke(this);
    }

    public void OnUpdate(float deltaTime) {
        if (Pause || Current <= 0) return;

        Current -= deltaTime;
        _secondOffset -= deltaTime;

        if (_secondOffset <= 0) {
            OnSecond?.Invoke(this);
            _secondOffset = 1;
        }
    }

    public void Clear() {
        _loop = false;
        _current = 0;
    }
    
    #region GetString

    public string GetTimeString() {
        int time = (int)Current;
        int min = time / 60;
        int sec = time % 60;
        return $"{min:00}:{sec:00}";
    }

    public string GetSecondString() {
        return $"{(int)Current}";
    }
    
    #endregion
    
} 
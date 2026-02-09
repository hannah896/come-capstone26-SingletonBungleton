using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 실제 시간(UTC)을 기준으로 지난시간, 남은시간 등을 계산해주는 매니저.
/// NyoTimer를 통해 게임 내 타이머를 관리합니다.
/// </summary>
public class TimeManager : CoreManager
{
    #region Fields

    // 타이머 컬렉션
    private Dictionary<string, NyoTimer> _timers = new();

    #endregion

    #region Properties

    // 일시정지 여부
    public bool Pause { get; set; }

    #endregion

    #region Indexer

    // 키로 타이머 접근
    public NyoTimer this[string key] => _timers.GetValueOrDefault(key);

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        GameEvents.OnGamePause += () => Pause = true;
        GameEvents.OnGameResume += () => Pause = false;
    }

    #endregion

    #region Timer Management

    /// <summary>
    /// 새 타이머를 생성합니다.
    /// </summary>
    public NyoTimer NewTimer(string key, float time, bool isAutoDestroy = false, bool start = true, bool loop = false)
    {
        if (_timers.TryGetValue(key, out NyoTimer timer)) timer.Clear();
        NyoTimer newTimer = new NyoTimer(key, time, isAutoDestroy, start, loop);
        _timers[key] = newTimer;
        newTimer.OnDestroy += t =>
        {
            _timers.Remove(key);
        };
        return newTimer;
    }

    /// <summary>
    /// 모든 타이머에 시간을 추가합니다.
    /// </summary>
    public void AddAllTime(float time, string[] excludes = null)
    {
        IEnumerable<NyoTimer> timers =
            excludes == null ? _timers.Values : _timers.Values.Where(t => !excludes.Contains(t.Key));
        foreach (NyoTimer timer in timers) timer.Add(time);
    }

    /// <summary>
    /// 매 프레임 타이머를 업데이트합니다.
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        if (Pause) return;
        if (GameScene.GameState != GameState.Playing) return;

        List<NyoTimer> timers = new(_timers.Values);
        foreach (NyoTimer timer in timers)
        {
            timer.OnUpdate(deltaTime);
        }
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        foreach (NyoTimer timer in _timers.Values) timer.Clear();
        _timers.Clear();
        Pause = false;
    }

    #endregion
}

/// <summary>
/// 게임 내 타이머 클래스.
/// 시간 경과, 반복, 자동 파괴 기능을 제공합니다.
/// </summary>
public class NyoTimer
{
    #region Fields

    // 현재 시간
    private float _current;

    // 반복 여부
    private bool _loop;

    // 자동 파괴 여부
    private bool _isAutoDestroy;

    // 초 단위까지 남은 시간
    private float _secondOffset;

    #endregion

    #region Properties

    // 타이머 키
    public string Key { get; private set; }

    // 설정된 시간
    public float Time { get; private set; }

    // 일시정지 여부
    public bool Pause { get; set; }

    // 현재 남은 시간
    public float Current
    {
        get => _current;
        set
        {
            _current = value;
            OnChangedTime?.Invoke(this);

            if (_current <= 0)
            {
                OnTimeEnd?.Invoke(this);
                if (_loop) Current = Time;
                else if (_isAutoDestroy) OnDestroy?.Invoke(this);
            }
        }
    }

    #endregion

    #region Events

    // 시간 변경 시 발생
    public event Action<NyoTimer> OnChangedTime;

    // 매 초 발생
    public event Action<NyoTimer> OnSecond;

    // 시간 종료 시 발생
    public event Action<NyoTimer> OnTimeEnd;

    // 파괴 시 발생
    public event Action<NyoTimer> OnDestroy;

    #endregion

    #region Constructor

    public NyoTimer(string key, float time, bool isAutoDestroy = false, bool start = true, bool loop = false)
    {
        Key = key;
        Time = time;
        Pause = !start;
        _loop = loop;
        _isAutoDestroy = isAutoDestroy;

        _current = Time;
        _secondOffset = Time % 1;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 시간을 추가합니다.
    /// </summary>
    public void Add(float time)
    {
        Current += time;
        OnSecond?.Invoke(this);
    }

    /// <summary>
    /// 매 프레임 업데이트합니다.
    /// </summary>
    public void OnUpdate(float deltaTime)
    {
        if (Pause || Current <= 0) return;

        Current -= deltaTime;
        _secondOffset -= deltaTime;

        if (_secondOffset <= 0)
        {
            OnSecond?.Invoke(this);
            _secondOffset = 1;
        }
    }

    /// <summary>
    /// 타이머를 초기화합니다.
    /// </summary>
    public void Clear()
    {
        _loop = false;
        _current = 0;
    }

    #endregion

    #region String Formatting

    /// <summary>
    /// MM:SS 형식의 시간 문자열을 반환합니다.
    /// </summary>
    public string GetTimeString()
    {
        int time = (int)Current;
        int min = time / 60;
        int sec = time % 60;
        return $"{min:00}:{sec:00}";
    }

    /// <summary>
    /// 초 단위 문자열을 반환합니다.
    /// </summary>
    public string GetSecondString()
    {
        return $"{(int)Current}";
    }

    #endregion
}

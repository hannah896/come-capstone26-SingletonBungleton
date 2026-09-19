using System;
using UnityEngine;

// 4단계 시간대
public enum TimePhase
{
    Dawn,       // 새벽 (00:00 ~ 06:00)
    Morning,    // 아침 (06:00 ~ 12:00)
    Afternoon,  // 오후 (12:00 ~ 18:00)
    Dusk        // 황혼 (18:00 ~ 24:00)
}

// [달 주기 시스템 추가] 8단계 달 위상 (8일 주기)
public enum MoonPhase
{
    New,            // 삭 (0)
    WaxingCrescent, // 초승 (1)
    FirstQuarter,   // 상현 (2)
    WaxingGibbous,  // 상현망간 (3)
    Full,           // 보름 (4)
    WaningGibbous,  // 하현망간 (5)
    LastQuarter,    // 하현 (6)
    WaningCrescent  // 그믐 (7)
}
public class WorldClock : MonoBehaviour
{
    public static WorldClock Instance { get; private set; }

    [Header("Time Settings")]
    private const float SECONDS_PER_DAY = 1440f; // 기본값: 현실 24분
    
    private const int MOON_CYCLE_DAYS = 8;

    private NyoTimer _dayTimer;
    private int _daysPassed = 0;
    private int _lastHour = -1;

    public TimePhase CurrentTimePhase { get; private set; }
    public int CurrentDay => _daysPassed + 1; // 0부터 시작하므로 +1

    /// <summary>경과 일수(0부터). 네트워크 복제 시 날짜와 하루 중 시간을 나눠 보내 float 정밀도 손실을 피한다.</summary>
    public int DaysPassed => _daysPassed;

    /// <summary>오늘 하루 중 경과한 초 (0 ~ SECONDS_PER_DAY)</summary>
    public float ElapsedSecondsToday => _dayTimer == null ? 0f : SECONDS_PER_DAY - _dayTimer.Current;

    /// <summary>
    /// 멀티 세션에서 네트워크가 시간을 구동하는지 여부.
    /// true면 로컬 NyoTimer 흐름을 멈추고 <see cref="SyncTime"/>으로만 시간이 바뀐다.
    /// (TimeManager는 사망/일시정지 등 GameState에 따라 멈추므로 피어마다 시간이 어긋나기 때문)
    /// </summary>
    public bool IsNetworkDriven { get; private set; }
    public int CurrentHour { get; private set; }

    /// <summary>
    /// 월드 시간 배속 (디버그/테스트용, 기본 1). 이동·몬스터 등 게임 속도와 무관하게 시계만 빨라진다.
    /// 멀티에서는 호스트 값이 기준이며, 클라이언트가 바꾸면 호스트에 요청해 전원에게 적용된다.
    /// </summary>
    public float TimeScale { get; private set; } = 1f;

    public const float MaxTimeScale = 120f;
    public MoonPhase CurrentMoonPhase { get; private set; } // [달 주기 시스템 추가] 현재 달 위상

    /// <summary>자정 0, 정오 0.5인 하루 진행률. 정수 시간과 달리 매 프레임 변한다.</summary>
    public float NormalizedTimeOfDay => _dayTimer == null
        ? 0.25f
        : Mathf.Repeat((SECONDS_PER_DAY - _dayTimer.Current) / SECONDS_PER_DAY, 1f);

    // 자원 재생, 특정 시간 조건 등에 사용할 '절대 시간(Timestamp)'
    public float TotalInGameSeconds
    {
        get
        {
            if (_dayTimer == null) return 0f;
            // NyoTimer는 카운트'다운'이므로 전체 시간에서 남은 시간을 빼서 경과 시간을 구함
            float timeOfDay = SECONDS_PER_DAY - _dayTimer.Current;
            return (_daysPassed * SECONDS_PER_DAY) + timeOfDay;
        }
    }

    // 다른 시스템들이 구독할 이벤트 (라이팅 변경, 몬스터 스폰 등)
    public event Action<TimePhase> OnPhaseChanged;
    public event Action<int> OnDayPassed;
    public event Action<int> OnHourPassed; // 인게임 1시간 경과 알림

    // [달 주기 시스템 추가] 달 위상 변경 알림
    public event Action<MoonPhase> OnMoonPhaseChanged; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() => EnsureInitialized();

    /// <summary>씬의 Start 호출 순서와 무관하게 로드 시계를 준비한다.</summary>
    public void EnsureInitialized()
    {
        if (_dayTimer != null) return;
        // 1. 프레임워크의 TimeManager에게 하루 길이의 무한 루프 타이머 발급 요청
        _dayTimer = Main.Time.NewTimer("WorldClockTimer", SECONDS_PER_DAY, isAutoDestroy: false, start: true, loop: true);

        // 2. 게임 시작 시간을 '아침(06:00)'으로 강제 세팅 (하루의 25% 경과 = 남은 시간 75%)
        _dayTimer.Current = SECONDS_PER_DAY * 0.75f;

        // Start 이전에 네트워크 구동이 지정됐다면 로컬 흐름을 멈춘다.
        _dayTimer.Pause = IsNetworkDriven;

        // 3. 타이머 이벤트 구독
        _dayTimer.OnTimeEnd += HandleDayEnded;
        _dayTimer.OnSecond += CheckTimeFlow;

        // 시작 직후 현재 상태 초기화
        CheckTimeFlow(_dayTimer);
        UpdateMoonPhase(true);

        Main.Loop.OnUpdate += OnUpdate;
    }

    public void SetTimeScale(float scale)
    {
        scale = Mathf.Clamp(scale, 1f, MaxTimeScale);

#if PHOTON_FUSION
        // 멀티 클라이언트는 호스트에 요청한다. 표시는 바로 바꾸고, 확정 값은 복제로 돌아온다.
        if (IsNetworkDriven && Main.Network != null && !Main.Network.IsHost)
        {
            TimeScale = scale;
            Main.Network.LocalPlayerData?.Rpc_RequestTimeScale(scale);
            return;
        }
#endif

        TimeScale = scale;
    }

    /// <summary>호스트가 확정한 배속을 반영한다. (네트워크 복제 전용 — 요청을 다시 보내지 않는다)</summary>
    public void SyncTimeScale(float scale)
    {
        TimeScale = Mathf.Clamp(scale, 1f, MaxTimeScale);
    }

    // 배속 분만큼 추가로 시간을 흘린다 (기본 1배 흐름은 NyoTimer가 담당).
    // 네트워크 구동 중에는 호스트가 틱마다 배속을 곱해 전진시키므로 여기서는 건드리지 않는다.
    private void OnUpdate(float deltaTime)
    {
        if (_dayTimer != null)
            _dayTimer.Pause = IsNetworkDriven || (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing));
        if (TimeScale <= 1f || IsNetworkDriven || _dayTimer == null || _dayTimer.Pause) return;
        if (Main.Time.Pause || GameScene.GameState != GameState.Playing) return; // NyoTimer와 같은 정지 조건

        SkipTime(deltaTime * (TimeScale - 1f));
    }

    private void HandleDayEnded(NyoTimer timer)
    {
        _daysPassed++;
        OnDayPassed?.Invoke(_daysPassed);
        UpdateMoonPhase(false);
    }

    private void CheckTimeFlow(NyoTimer timer)
    {
        float timeOfDay = SECONDS_PER_DAY - timer.Current;

        // --- 1. 인게임 시간(Hour) 체크 ---
        // (하루 진행률을 24시간으로 변환)
        int currentHour = Mathf.FloorToInt((timeOfDay / SECONDS_PER_DAY) * 24f);
        CurrentHour = currentHour;
        if (currentHour != _lastHour)
        {
            _lastHour = currentHour;
            OnHourPassed?.Invoke(currentHour);
        }

        // --- 2. 시간대(Phase) 갱신 ---
        TimePhase newPhase = CurrentTimePhase;

        if (timeOfDay >= SECONDS_PER_DAY * 0.75f) newPhase = TimePhase.Dusk;
        else if (timeOfDay >= SECONDS_PER_DAY * 0.50f) newPhase = TimePhase.Afternoon;
        else if (timeOfDay >= SECONDS_PER_DAY * 0.25f) newPhase = TimePhase.Morning;
        else newPhase = TimePhase.Dawn;

        if (newPhase != CurrentTimePhase)
        {
            CurrentTimePhase = newPhase;
            OnPhaseChanged?.Invoke(CurrentTimePhase);
            // Debug.Log($"🌞 시간대 변경: {CurrentTimePhase}");
        }
    }

    private void UpdateMoonPhase(bool forceInvoke)
    {
        int phaseIndex = _daysPassed % MOON_CYCLE_DAYS;
        MoonPhase newPhase = (MoonPhase)phaseIndex;

        if (forceInvoke || newPhase != CurrentMoonPhase)
        {
            CurrentMoonPhase = newPhase;
            OnMoonPhaseChanged?.Invoke(CurrentMoonPhase);
        }
    }

    /// <summary>
    /// 게임 내 시간을 강제로 경과시킵니다. (예: 침대 수면 시 8시간 스킵)
    /// </summary>
    public void SkipTime(float skipSeconds)
    {
        if (_dayTimer == null || skipSeconds <= 0f) return;

#if PHOTON_FUSION
        // 멀티 클라이언트는 시간을 직접 바꾸지 않고 호스트에 요청한다. 결과는 복제로 돌아온다.
        if (IsNetworkDriven && Main.Network != null && !Main.Network.IsHost)
        {
            Main.Network.LocalPlayerData?.Rpc_RequestSkipTime(skipSeconds);
            return;
        }
#endif

        float elapsedToday = SECONDS_PER_DAY - _dayTimer.Current;
        float totalElapsed = elapsedToday + skipSeconds;

        int passedDays = Mathf.FloorToInt(totalElapsed / SECONDS_PER_DAY);
        float newElapsedToday = totalElapsed % SECONDS_PER_DAY;

        for (int i = 0; i < passedDays; i++)
        {
            _daysPassed++;
            OnDayPassed?.Invoke(_daysPassed);
        }

        _dayTimer.Current = SECONDS_PER_DAY - newElapsedToday;
        CheckTimeFlow(_dayTimer);
        UpdateMoonPhase(false);
    }


    // ==========================================
    // [네트워크 동기화]
    // ==========================================

    /// <summary>
    /// 네트워크 구동 여부를 설정한다. 구동 중에는 로컬 타이머가 스스로 흐르지 않는다.
    /// 세션이 끝나면 false로 되돌려 로컬 흐름을 재개한다.
    /// </summary>
    public void SetNetworkDriven(bool driven)
    {
        IsNetworkDriven = driven;
        if (_dayTimer != null) _dayTimer.Pause = driven;
    }

    /// <summary>
    /// 지정한 시각으로 맞추고 날짜/시간/시간대/달 위상 이벤트를 발행한다.
    /// 호스트는 틱마다 자기 시간을 전진시킬 때, 클라이언트는 복제된 호스트 시간을 반영할 때 호출한다.
    /// </summary>
    public void SyncTime(int daysPassed, float elapsedSecondsToday)
    {
        if (_dayTimer == null) return;

        // 하루를 넘긴 값이 들어와도 날짜로 정규화한다.
        if (elapsedSecondsToday >= SECONDS_PER_DAY || elapsedSecondsToday < 0f)
        {
            int overflowDays = Mathf.FloorToInt(elapsedSecondsToday / SECONDS_PER_DAY);
            daysPassed += overflowDays;
            elapsedSecondsToday -= overflowDays * SECONDS_PER_DAY;
        }
        daysPassed = Mathf.Max(0, daysPassed);

        bool dayChanged = daysPassed != _daysPassed;

        // 앞으로 넘어간 날짜는 하루씩 알린다 (수면 스킵으로 여러 날이 지나도 누락 없이).
        while (_daysPassed < daysPassed)
        {
            _daysPassed++;
            OnDayPassed?.Invoke(_daysPassed);
        }

        // 되감긴 경우(늦게 도착한 호스트 시간이 로컬 초기값보다 이른 날짜)는 한 번만 알린다.
        if (_daysPassed > daysPassed)
        {
            _daysPassed = daysPassed;
            OnDayPassed?.Invoke(_daysPassed);
        }

        // 남은 시간은 (0, SECONDS_PER_DAY] 범위라 OnTimeEnd가 중복 발행되지 않는다.
        _dayTimer.Current = SECONDS_PER_DAY - elapsedSecondsToday;

        CheckTimeFlow(_dayTimer);
        if (dayChanged) UpdateMoonPhase(false);
    }


    // ==========================================
    // [세이브 & 로드 시스템]
    // ==========================================

    /// <summary>
    /// 세이브할 때 호출: 이 float 값 하나만 디스크(JSON 등)에 저장하면 됨.
    /// </summary>
    public float SaveTime()
    {
        return TotalInGameSeconds;
    }

    /// <summary>
    /// 로드할 때 호출: 저장해둔 float 값을 그대로 
    /// </summary>
    public void LoadTime(float savedTotalSeconds)
    {
        if (float.IsNaN(savedTotalSeconds) || float.IsInfinity(savedTotalSeconds) || savedTotalSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(savedTotalSeconds));
        EnsureInitialized();

        // 1. 누적 시간을 기준으로 '몇 일차'인지 계산하여 복구
        _daysPassed = Mathf.FloorToInt(savedTotalSeconds / SECONDS_PER_DAY);

        // 2. 누적 시간을 기준으로 '오늘 하루 중 몇 초가 지났는지' 계산
        float timeOfDay = savedTotalSeconds % SECONDS_PER_DAY;

        // 3. NyoTimer는 카운트'다운' 타이머이므로, (전체시간 - 진행된시간)을 남은 시간으로 세팅!
        _dayTimer.Current = SECONDS_PER_DAY - timeOfDay;

        // 4. 로드 직후 UI 및 이벤트 즉시 동기화
        OnDayPassed?.Invoke(_daysPassed);
        CheckTimeFlow(_dayTimer);
        UpdateMoonPhase(true);

        Debug.Log($"💾 시간 로드 완료: {_daysPassed + 1}일차 {CurrentTimePhase}에 접속하셨습니다.");
    }

    private void OnDestroy()
    {
        if (Main.Instance != null && Main.Loop != null) Main.Loop.OnUpdate -= OnUpdate;

        if (_dayTimer != null)
        {
            _dayTimer.OnTimeEnd -= HandleDayEnded;
            _dayTimer.OnSecond -= CheckTimeFlow;
        }

        if (Instance == this) Instance = null;

    }
}

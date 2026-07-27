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
    [Tooltip("현실 24분 = 인게임 1일 (초 단위)")]
    private const float SECONDS_PER_DAY = 1440f;
    private const int MOON_CYCLE_DAYS = 8;

    private NyoTimer _dayTimer;
    private int _daysPassed = 0;
    private int _lastHour = -1;

    public TimePhase CurrentTimePhase { get; private set; }
    public int CurrentDay => _daysPassed + 1; // 0부터 시작하므로 +1
    public int CurrentHour { get; private set; }
    public MoonPhase CurrentMoonPhase { get; private set; } // [달 주기 시스템 추가] 현재 달 위상

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
    public event Action<int> OnHourPassed; // 인게임 1시간(현실 1분) 경과 알림

    // [달 주기 시스템 추가] 달 위상 변경 알림
    public event Action<MoonPhase> OnMoonPhaseChanged; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 1. 프레임워크의 TimeManager에게 1440초짜리 무한 루프 타이머 발급 요청
        _dayTimer = Main.Time.NewTimer("WorldClockTimer", SECONDS_PER_DAY, isAutoDestroy: false, start: true, loop: true);

        // 2. 게임 시작 시간을 '아침(06:00)'으로 강제 세팅 (1440초의 25% 경과 = 남은 시간 75%)
        _dayTimer.Current = SECONDS_PER_DAY * 0.75f;

        // 3. 타이머 이벤트 구독
        _dayTimer.OnTimeEnd += HandleDayEnded;
        _dayTimer.OnSecond += CheckTimeFlow;

        // 시작 직후 현재 상태 초기화
        CheckTimeFlow(_dayTimer);
        UpdateMoonPhase(true);
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
        // (0 ~ 1439초를 24시간으로 변환)
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
        if (_dayTimer == null) return;

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
        if (_dayTimer != null)
        {
            _dayTimer.OnTimeEnd -= HandleDayEnded;
            _dayTimer.OnSecond -= CheckTimeFlow;
        }

        if (Instance == this) Instance = null;

    }
}
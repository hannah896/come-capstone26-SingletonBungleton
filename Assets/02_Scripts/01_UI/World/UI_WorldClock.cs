using UnityEngine;

/// <summary>
/// <see cref="WorldClock"/>의 상태를 화면에 표시하는 View다.
/// 시간 계산과 저장은 WorldClock이 담당하며 이 클래스는 이벤트를 구독해 표시만 갱신한다.
/// </summary>
public class UI_WorldClock : UI
{
    [Header("Texts")]
    [SerializeField] private UI_Text _dayText;
    [SerializeField] private UI_Text _timeText;
    [SerializeField] private UI_Text _phaseText;
    [SerializeField] private UI_Text _moonText;

    [Header("Formats")]
    [SerializeField] private string _dayFormat = "Day {0}";
    [SerializeField] private string _timeFormat = "{0:00}:00";
    [SerializeField] private string _phaseFormat = "{0}";
    [SerializeField] private string _moonFormat = "{0}";

    private WorldClock _worldClock;

    public override bool Initialize()
    {
        return base.Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
    }

    private void Update()
    {
        if (_worldClock == null)
        {
            TryBind();
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>표시할 월드 시계를 바인딩한다.</summary>
    public void Bind(WorldClock worldClock)
    {
        if (_worldClock == worldClock)
        {
            RefreshAll();
            return;
        }

        Unbind();
        if (worldClock == null) return;

        _worldClock = worldClock;
        _worldClock.OnDayPassed += HandleDayPassed;
        _worldClock.OnHourPassed += HandleHourPassed;
        _worldClock.OnPhaseChanged += HandlePhaseChanged;
        _worldClock.OnMoonPhaseChanged += HandleMoonPhaseChanged;

        RefreshAll();
    }

    /// <summary>현재 바인딩을 해제한다.</summary>
    public void Unbind()
    {
        if (_worldClock == null) return;

        _worldClock.OnDayPassed -= HandleDayPassed;
        _worldClock.OnHourPassed -= HandleHourPassed;
        _worldClock.OnPhaseChanged -= HandlePhaseChanged;
        _worldClock.OnMoonPhaseChanged -= HandleMoonPhaseChanged;
        _worldClock = null;
    }

    private void TryBind()
    {
        if (WorldClock.Instance != null)
        {
            Bind(WorldClock.Instance);
        }
    }

    private void RefreshAll()
    {
        if (_worldClock == null) return;

        SetDay(_worldClock.CurrentDay);
        SetHour(_worldClock.CurrentHour);
        SetPhase(_worldClock.CurrentTimePhase);
        SetMoonPhase(_worldClock.CurrentMoonPhase);
    }

    private void HandleDayPassed(int _)
    {
        // 이벤트 인수는 0부터 누적된 일수이므로 표시용 CurrentDay를 사용한다.
        SetDay(_worldClock.CurrentDay);
    }

    private void HandleHourPassed(int hour) => SetHour(hour);
    private void HandlePhaseChanged(TimePhase phase) => SetPhase(phase);
    private void HandleMoonPhaseChanged(MoonPhase phase) => SetMoonPhase(phase);

    private void SetDay(int day)
    {
        if (_dayText != null)
            _dayText.Text = string.Format(_dayFormat, day);
    }

    private void SetHour(int hour)
    {
        if (_timeText != null)
            _timeText.Text = string.Format(_timeFormat, hour);
    }

    private void SetPhase(TimePhase phase)
    {
        if (_phaseText != null)
            _phaseText.Text = string.Format(_phaseFormat, phase);
    }

    private void SetMoonPhase(MoonPhase phase)
    {
        if (_moonText != null)
            _moonText.Text = string.Format(_moonFormat, phase);
    }
}

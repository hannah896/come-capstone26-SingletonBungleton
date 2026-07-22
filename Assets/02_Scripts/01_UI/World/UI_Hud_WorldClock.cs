using UnityEngine;

public class UI_Hud_WorldClock : UI_Hud
{
    #region Field
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

    #endregion


    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        return true;
    }

    private void OnEnable()
    {
        Initialize();
        Bind(WorldClock.Instance);
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Bind(WorldClock worldClock)
    {
        if (worldClock == null) return;
        if (_worldClock == worldClock) return;

        Unbind();
        _worldClock = worldClock;

        _worldClock.OnDayPassed += HandleDayPassed;
        _worldClock.OnHourPassed += HandleHourPassed;
        _worldClock.OnPhaseChanged += HandlePhaseChanged;
        _worldClock.OnMoonPhaseChanged += HandleMoonPhaseChanged;

        RefreshAll();
    }

    private void Unbind()
    {
        if (_worldClock == null) return;

        _worldClock.OnDayPassed -= HandleDayPassed;
        _worldClock.OnHourPassed -= HandleHourPassed;
        _worldClock.OnPhaseChanged -= HandlePhaseChanged;
        _worldClock.OnMoonPhaseChanged -= HandleMoonPhaseChanged;

        _worldClock = null;
    }

    private void RefreshAll()
    {
        if (_worldClock == null) return;

        HandleDayPassed(_worldClock.CurrentDay);
        HandleHourPassed(_worldClock.CurrentHour);
        HandlePhaseChanged(_worldClock.CurrentTimePhase);
        HandleMoonPhaseChanged(_worldClock.CurrentMoonPhase);
    }

    private void HandleDayPassed(int day)
    {
        if (_dayText == null) return;

        _dayText.Text = string.Format(_dayFormat, day);
    }

    private void HandleHourPassed(int hour)
    {
        if (_timeText == null) return;

        _timeText.Text = string.Format(_timeFormat, hour);
    }

    private void HandlePhaseChanged(TimePhase phase)
    {
        if (_phaseText == null) return;

        _phaseText.Text = string.Format(_phaseFormat, phase);
    }

    private void HandleMoonPhaseChanged(MoonPhase phase)
    {
        if (_moonText == null) return;

        _moonText.Text = string.Format(_moonFormat, phase);
    }
}

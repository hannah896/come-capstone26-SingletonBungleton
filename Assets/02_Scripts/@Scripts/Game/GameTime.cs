using System.Collections.Generic;

/// <summary>
/// 게임 내 수동 시간 흐름을 관리하는 클래스.
/// 플레이어가 시간을 진행시키면, 등록된 IUseTimer 구현체들에게 경과 시간(분)을 전달합니다.
/// Main.Game.Time으로 접근합니다.
/// </summary>
public class GameTime
{
    #region Fields

    // 등록된 타이머 사용자 목록
    private readonly List<IUseTimer> _timerUsers = new();

    // 총 경과 시간 (분)
    private int _totalElapsedMinutes;

    #endregion

    #region Properties

    // 총 경과 시간 (분)
    public int TotalElapsedMinutes => _totalElapsedMinutes;

    #endregion

    #region Public Methods

    /// <summary>
    /// 타이머 사용자를 등록합니다.
    /// </summary>
    public void RegisterTimer(IUseTimer timer)
    {
        if (!_timerUsers.Contains(timer))
            _timerUsers.Add(timer);
    }

    /// <summary>
    /// 타이머 사용자를 해제합니다.
    /// </summary>
    public void UnregisterTimer(IUseTimer timer)
    {
        _timerUsers.Remove(timer);
    }

    /// <summary>
    /// 시간을 진행시킵니다. 등록된 모든 IUseTimer에게 경과 시간(분)을 전달합니다.
    /// </summary>
    /// <param name="minutes">진행할 시간 (분)</param>
    public void AdvanceTime(int minutes)
    {
        if (minutes <= 0) return;

        _totalElapsedMinutes += minutes;

        for (int i = _timerUsers.Count - 1; i >= 0; i--)
        {
            _timerUsers[i].ApplyTimer(minutes);
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 모든 타이머 등록을 해제하고 경과 시간을 초기화합니다.
    /// </summary>
    public void Clear()
    {
        _timerUsers.Clear();
        _totalElapsedMinutes = 0;
    }

    #endregion
}

#region Timer Interface

/// <summary>
/// 시간 경과에 반응하는 시스템이 구현하는 인터페이스.
/// GameTime.RegisterTimer()로 등록하면 AdvanceTime() 호출 시 ApplyTimer()가 실행됩니다.
/// </summary>
public interface IUseTimer
{
    /// <summary>
    /// 경과된 시간(분)을 받아 처리합니다.
    /// </summary>
    /// <param name="minutes">경과 시간 (분)</param>
    void ApplyTimer(int minutes);
}

#endregion

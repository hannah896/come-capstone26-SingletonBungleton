/// <summary>
/// GameManager의 자주 사용하는 메서드들을 정적 메서드로 제공하는 유틸리티 클래스.
/// Main.Game.Time에 대한 단축 접근을 제공합니다.
/// </summary>
public static class GameExtensions
{
    #region Timer

    /// <summary>
    /// 타이머 사용자를 등록합니다.
    /// </summary>
    public static void RegisterTimer(IUseTimer timer) => Main.Game.Time.RegisterTimer(timer);

    /// <summary>
    /// 타이머 사용자를 해제합니다.
    /// </summary>
    public static void UnregisterTimer(IUseTimer timer) => Main.Game.Time.UnregisterTimer(timer);

    /// <summary>
    /// 시간을 진행시킵니다. 등록된 모든 IUseTimer에게 경과 시간(분)을 전달합니다.
    /// </summary>
    public static void AdvanceTime(int minutes) => Main.Game.Time.AdvanceTime(minutes);

    /// <summary>
    /// 총 경과 시간(분)을 반환합니다.
    /// </summary>
    public static int TotalElapsedMinutes => Main.Game.Time.TotalElapsedMinutes;

    #endregion
}

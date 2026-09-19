using UnityEngine;

/// <summary>저장/복원 중 대기 시간은 아이템의 유통기한에서 제외한다.</summary>
public static class SavePlayClock
{
    private static bool paused;
    private static float pausedAt;
    private static float pausedSeconds;
    public static float Now => (paused ? pausedAt : Time.time) - pausedSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { paused = false; pausedAt = pausedSeconds = 0f; }

    public static void SetPaused(bool value)
    {
        if (paused == value) return;
        if (value) pausedAt = Time.time;
        else pausedSeconds += Mathf.Max(0f, Time.time - pausedAt);
        paused = value;
    }
}

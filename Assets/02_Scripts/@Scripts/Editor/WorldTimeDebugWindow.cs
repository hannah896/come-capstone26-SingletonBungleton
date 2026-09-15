#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 런타임 중 월드 시간(낮/밤 시계)을 통제하는 디버그 에디터 툴.
/// - 시간 배속 (시계만 빨라지고 이동/몬스터 등 게임 속도는 그대로)
/// - 시간 건너뛰기 (+1시간 / +6시간 / 다음 시간대 / +1일)
/// 메뉴: Tools/World/Time Debug
/// </summary>
public class WorldTimeDebugWindow : EditorWindow
{
    // 인게임 1시간 = 현실 60초 (하루 1440초 / 24시간)
    private const float SecondsPerHour = 60f;
    private const float SecondsPerDay = 1440f;

    private static readonly float[] ScalePresets = { 1f, 2f, 5f, 10f, 30f, 60f, 120f };

    [MenuItem("Tools/World/Time Debug")]
    public static void Open()
    {
        GetWindow<WorldTimeDebugWindow>("World Time Debug");
    }

    // 런타임 값이 실시간으로 보이도록 주기적 리페인트
    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode에서만 동작합니다.", MessageType.Info);
            return;
        }

        WorldClock clock = WorldClock.Instance;
        if (clock == null)
        {
            EditorGUILayout.HelpBox("WorldClock이 없습니다. 월드 생성이 끝난 뒤 사용할 수 있습니다.", MessageType.Warning);
            return;
        }

        DrawStatus(clock);
        EditorGUILayout.Space();
        DrawTimeScale(clock);
        EditorGUILayout.Space();
        DrawSkip(clock);
    }

    private static void DrawStatus(WorldClock clock)
    {
        EditorGUILayout.LabelField("현재 시간", EditorStyles.boldLabel);

        float elapsed = clock.ElapsedSecondsToday;
        int hour = Mathf.FloorToInt(elapsed / SecondsPerHour);
        int minute = Mathf.FloorToInt(elapsed % SecondsPerHour);

        EditorGUILayout.LabelField("날짜", $"{clock.CurrentDay}일차");
        EditorGUILayout.LabelField("시각", $"{hour:00}:{minute:00}");
        EditorGUILayout.LabelField("시간대", clock.CurrentTimePhase.ToString());
        EditorGUILayout.LabelField("달 위상", clock.CurrentMoonPhase.ToString());

        if (GameScene.GameState != GameState.Playing)
            EditorGUILayout.HelpBox($"GameState가 {GameScene.GameState}라 시간이 멈춰 있습니다.", MessageType.None);
    }

    private static void DrawTimeScale(WorldClock clock)
    {
        EditorGUILayout.LabelField("시간 배속", EditorStyles.boldLabel);

        if (IsRemoteClient(clock))
        {
            EditorGUILayout.HelpBox("멀티 클라이언트입니다. 배속/건너뛰기는 호스트에 요청되어 모든 플레이어에게 적용됩니다.", MessageType.Info);
        }

        float scale = EditorGUILayout.Slider("배속", clock.TimeScale, 1f, WorldClock.MaxTimeScale);
        if (!Mathf.Approximately(scale, clock.TimeScale))
            clock.SetTimeScale(scale);

        EditorGUILayout.BeginHorizontal();
        foreach (float preset in ScalePresets)
        {
            if (GUILayout.Button($"x{preset:0}"))
                clock.SetTimeScale(preset);
        }
        EditorGUILayout.EndHorizontal();

        float realSecondsPerDay = SecondsPerDay / clock.TimeScale;
        EditorGUILayout.LabelField("하루 길이(현실)", $"{realSecondsPerDay / 60f:0.##}분 ({realSecondsPerDay:0.#}초)");
    }

    private static void DrawSkip(WorldClock clock)
    {
        EditorGUILayout.LabelField("시간 건너뛰기", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+1시간")) clock.SkipTime(SecondsPerHour);
        if (GUILayout.Button("+6시간")) clock.SkipTime(SecondsPerHour * 6f);
        if (GUILayout.Button("다음 시간대")) clock.SkipTime(GetSecondsToNextPhase(clock));
        if (GUILayout.Button("+1일")) clock.SkipTime(SecondsPerDay);
        EditorGUILayout.EndHorizontal();
    }

    // 시간대는 하루를 4등분(6시간 단위)한다
    private static float GetSecondsToNextPhase(WorldClock clock)
    {
        float phaseLength = SecondsPerDay * 0.25f;
        float elapsed = clock.ElapsedSecondsToday;
        float next = (Mathf.Floor(elapsed / phaseLength) + 1f) * phaseLength;
        return Mathf.Max(0.01f, next - elapsed);
    }

    private static bool IsRemoteClient(WorldClock clock)
    {
#if PHOTON_FUSION
        return clock.IsNetworkDriven && Main.Network != null && !Main.Network.IsHost;
#else
        return false;
#endif
    }
}
#endif

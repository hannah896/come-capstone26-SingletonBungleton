#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 런타임 중 플레이어 스탯을 통제하는 디버그 에디터 툴.
/// - 무적 토글(체력 0이어도 사망하지 않음)
/// - 입력한 양만큼 HP/허기/Ego 회복 (또는 MAX 충전)
/// 메뉴: Tools/Player/Stat Debug
/// </summary>
public class PlayerStatDebugWindow : EditorWindow
{
    private float _hpAmount = 50f;
    private float _hungerAmount = 50f;
    private float _egoAmount = 50f;

    [MenuItem("Tools/Player/Stat Debug")]
    public static void Open()
    {
        GetWindow<PlayerStatDebugWindow>("Player Stat Debug");
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

        Player player = Object.FindFirstObjectByType<Player>();
        if (player == null || player.Stat == null)
        {
            EditorGUILayout.HelpBox("씬에서 Player를 찾을 수 없거나 스탯이 아직 초기화되지 않았습니다.", MessageType.Warning);
            return;
        }

        PlayerStatus stat = player.Stat;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("무적", EditorStyles.boldLabel);
        bool invincible = EditorGUILayout.ToggleLeft("체력이 0이어도 죽지 않음 (데미지/허기 무시)", stat.Invincible);
        if (invincible != stat.Invincible)
            stat.Invincible = invincible;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("현재 스탯", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"HP    : {stat.CurrentHp:F0} / {stat.MaxHp:F0}");
        EditorGUILayout.LabelField($"허기  : {stat.CurrentHunger:F0} / {stat.MaxHunger:F0}");
        EditorGUILayout.LabelField($"Ego   : {stat.CurrentEgo:F0} / {stat.MaxEgo:F0}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("스탯 채우기", EditorStyles.boldLabel);
        DrawRestoreRow("HP", ref _hpAmount, stat.RestoreHp, stat.MaxHp);
        DrawRestoreRow("허기", ref _hungerAmount, stat.RestoreHunger, stat.MaxHunger);
        DrawRestoreRow("Ego", ref _egoAmount, stat.RestoreEgo, stat.MaxEgo);

        EditorGUILayout.Space();
        if (GUILayout.Button("모두 MAX로 충전"))
        {
            stat.RestoreHp(stat.MaxHp);
            stat.RestoreHunger(stat.MaxHunger);
            stat.RestoreEgo(stat.MaxEgo);
        }
    }

    // 한 줄: [라벨] [입력 양] [채우기] [MAX]
    private void DrawRestoreRow(string label, ref float amount, System.Action<float> restore, float max)
    {
        EditorGUILayout.BeginHorizontal();
        amount = EditorGUILayout.FloatField(label, amount);
        if (GUILayout.Button("채우기", GUILayout.Width(60f)))
            restore(amount);
        if (GUILayout.Button("MAX", GUILayout.Width(50f)))
            restore(max);
        EditorGUILayout.EndHorizontal();
    }
}
#endif

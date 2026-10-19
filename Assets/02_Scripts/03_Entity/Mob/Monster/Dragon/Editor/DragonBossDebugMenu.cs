using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 서리룡 플레이 테스트용 메뉴 (플레이 모드 전용). 메뉴: Tools/최종 보스/테스트/...
/// - 소환: 로컬 플레이어 앞 20m에 서리룡을 띄운다
/// - 패턴 강제: 쿨타임과 상관없이 지정 패턴을 바로 쓴다 (AI가 고른 패턴과 섞이지 않게 쿨을 크게 잡는 메뉴도 있다)
/// </summary>
public static class DragonBossDebugMenu
{
    private const string Root = "Tools/최종 보스/테스트/";
    private const string StatFolder = "Assets/05_Datas/SO/MonsterStatData/";

    private static readonly FieldInfo MachineField =
        typeof(Monster).GetField("stateMachine", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo CooldownField =
        typeof(DragonBoss).GetField("cooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo TakeOffPendingField =
        typeof(DragonBoss).GetField("takeOffPending", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ShootdownField =
        typeof(DragonBoss).GetField("shootdownGauge", BindingFlags.NonPublic | BindingFlags.Instance);

    [MenuItem(Root + "서리룡 소환", false, 0)]
    private static void SpawnPolar() => Spawn("PolarDragon");

    [MenuItem(Root + "망령룡 소환 (등장 연출)", false, 1)]
    private static void SpawnUndead() => Spawn("UndeadDragon", intro: true);

    [MenuItem(Root + "보스전 시작 (서리룡 → 망령룡)", false, 2)]
    private static void StartEncounter()
    {
        if (!EditorApplication.isPlaying) return;
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null) return;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        var go = new GameObject("DebugFinalBossEncounter");
        go.transform.position = player.transform.position + forward.normalized * 20f;
        go.transform.rotation = Quaternion.LookRotation(-forward.normalized);
        go.AddComponent<FinalBossEncounter>().StartEncounter();
    }

    private static void Spawn(string key, bool intro = false)
    {
        if (!EditorApplication.isPlaying) return;
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("[DragonDebug] 플레이어 없음");
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        Vector3 pos = player.transform.position + forward.normalized * 20f;
        var stat = AssetDatabase.LoadAssetAtPath<DragonStatData>(StatFolder + key + ".asset");
        SpawnAsync(key, pos, stat, player, intro).Forget();
    }

    private static async UniTaskVoid SpawnAsync(string key, Vector3 pos, DragonStatData stat, Player player, bool intro)
    {
        var monster = await Monster.SpawnAsync(key, stat, pos);
        if (monster == null) return;
        Vector3 look = player.transform.position - pos;
        look.y = 0f;
        monster.transform.rotation = Quaternion.LookRotation(look);
        if (intro && monster is DragonBoss boss) boss.BeginIntro();
        Debug.Log($"[DragonDebug] {key} 소환 {pos}");
    }

    [MenuItem(Root + "AI 패턴 잠금 (쿨 999초)", false, 20)]
    private static void LockCooldowns() => SetCooldowns(999f);

    [MenuItem(Root + "AI 패턴 잠금 해제", false, 21)]
    private static void UnlockCooldowns() => SetCooldowns(0f);

    [MenuItem(Root + "패턴: 물기", false, 40)]   private static void Bite()   => Machine()?.ToSkill(DragonSkill.Bite);
    [MenuItem(Root + "패턴: 빙탄 3연사", false, 41)] private static void Spit() => Machine()?.ToSkill(DragonSkill.Spit);
    [MenuItem(Root + "패턴: 브레스", false, 42)] private static void Breath() => Machine()?.ToSkill(DragonSkill.Breath);
    [MenuItem(Root + "패턴: 돌진", false, 43)]   private static void Charge() => Machine()?.ToSkill(DragonSkill.Charge);
    [MenuItem(Root + "그로기", false, 44)]
    private static void Groggy()
    {
        var b = Boss();
        if (b != null && b.Data != null) Machine()?.ToGroggy(b.Data.GroggyDuration, b.Data.GroggyDamageMultiplier);
    }

    [MenuItem(Root + "최종 발악", false, 45)]
    private static void Frenzy() => Machine()?.ToFrenzy();

    [MenuItem(Root + "월식의 응시", false, 46)]
    private static void Gaze() => Machine()?.ToGaze();

    [MenuItem(Root + "망령의 각인", false, 47)]
    private static void Mark()
    {
        var b = Boss();
        if (b == null) return;
        b.ForceMarks();
        Debug.Log($"[DragonDebug] 각인 {b.ActiveMarkCount}명");
    }

    [MenuItem(Root + "테스트 기둥 4개 배치", false, 70)]
    private static void PlacePillars()
    {
        if (!EditorApplication.isPlaying) return;
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null) return;

        // 보스가 있으면 아레나 중심, 없으면 플레이어 앞 20m를 중심으로 반경 12m에 네 개
        var b = Object.FindAnyObjectByType<DragonBoss>();
        Vector3 center = b != null ? b.ArenaCenter : player.transform.position + player.transform.forward * 20f;
        center.y = player.transform.position.y;

        var root = new GameObject("DebugPillars");
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "MoonPillar_" + i;
            pillar.transform.SetParent(root.transform);
            pillar.transform.localScale = new Vector3(1.6f, 3f, 1.6f); // 높이 6m
            pillar.transform.position = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 12f + Vector3.up * 3f;
            pillar.AddComponent<DragonCover>();
        }
        Debug.Log($"[DragonDebug] 테스트 기둥 4개 배치 (중심 {center})");
    }

    [MenuItem(Root + "기둥 부식 초기화", false, 71)]
    private static void ResetPillars()
    {
        foreach (var cover in DragonCover.All)
            cover?.ResetCorrosion();
    }

    [MenuItem(Root + "이륙 (공중 페이즈)", false, 60)]
    private static void TakeOff()
    {
        var b = Boss();
        if (b == null) return;
        TakeOffPendingField.SetValue(b, true);
        Machine()?.ToTakeOff();
    }

    [MenuItem(Root + "격추 게이지 0 (추락)", false, 61)]
    private static void Shootdown()
    {
        var b = Boss();
        if (b != null && b.IsAirborne) ShootdownField.SetValue(b, 0f);
    }

    [MenuItem(Root + "격추 실패 (활공 → 폭발/폭우)", false, 62)]
    private static void Glide()
    {
        var b = Boss();
        if (b != null && b.IsAirborne) Machine()?.ToGlide();
    }

    [MenuItem(Root + "피해 로그 켜기·끄기", false, 81)]
    private static void ToggleHitLog()
    {
        DragonBoss.DebugLogHits = !DragonBoss.DebugLogHits;
        Debug.Log($"[DragonDebug] 피해 로그 {(DragonBoss.DebugLogHits ? "켬" : "끔")}");
    }

    [MenuItem(Root + "상태 출력", false, 80)]
    private static void Dump()
    {
        var b = Boss();
        if (b == null) return;
        var m = MachineField.GetValue(b) as DragonStateMachine;
        Debug.Log($"[DragonDebug] state={m?.CurrentState?.GetType().Name} anim={b.CurrentAnimId} hp={b.HpRatio:0.000} " +
                  $"gauge={b.PoiseRatio:0.00} air={b.IsAirborne} alt={b.Altitude:0.0} groggy={b.IsGroggy} pos={b.transform.position}");
    }

    private static DragonBoss Boss()
    {
        if (!EditorApplication.isPlaying) return null;
        var b = Object.FindAnyObjectByType<DragonBoss>();
        if (b == null) Debug.LogWarning("[DragonDebug] 서리룡 없음");
        return b;
    }

    private static DragonStateMachine Machine()
    {
        var b = Boss();
        return b != null ? MachineField.GetValue(b) as DragonStateMachine : null;
    }

    private static void SetCooldowns(float value)
    {
        var b = Boss();
        if (b == null) return;
        var arr = (float[])CooldownField.GetValue(b);
        for (int i = 0; i < arr.Length; i++) arr[i] = value;
    }
}

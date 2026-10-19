using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 몬스터 전투 측정 도구 (플레이 모드 전용). 메뉴: Tools/몬스터/전투 측정 (전체)
///
/// 몬스터마다 빈 곳에 플레이어와 1:1로 세워 두고
/// ① 공격 측정: ObserveSeconds 동안 플레이어가 받은 피해(도트 포함)를 매 프레임 기록하고 체력을 다시 채운다
///    → 총 피해 · 초당 피해 · 1회 최대 피해 · 실제로 쓴 행동(애니 ID)
/// ② 처치 측정: 검(35) 타격을 0.1초 간격으로 넣어 죽을 때까지 몇 타가 필요한지 센다
///    (방어력·무적·그로기 배율이 그대로 반영된다)
/// 결과는 콘솔과 Temp/MonsterCombatProbe.txt에 남는다. 플레이어는 위치가 고정되고 죽지 않는다.
/// </summary>
public static class MonsterCombatProbe
{
    private static readonly string[] Keys =
    {
        "Mischief", "TreantMinionEvergreen", "CyclopsBat", "Demon", "TreantTreeEvergreen", "CyclopsBatWizard",
        "Devil", "CyclopsBatMage", "Treeguard",
    };

    private static readonly HashSet<string> BossKeys = new HashSet<string> { "Devil", "CyclopsBatMage", "Treeguard" };

    private const float ObserveSeconds = 25f;
    private const float BossObserveSeconds = 50f;
    private const int SwordDamage = 35;           // 검 30 + 플레이어 기본 공격 5
    private const float SwordSwingSeconds = 1.467f;
    private static readonly Vector3 ArenaPos = new Vector3(300f, -1.63f, 300f);
    private const string ReportPath = "Temp/MonsterCombatProbe.txt";

    private static bool s_running;

    [MenuItem("Tools/몬스터/전투 측정 (전체)")]
    private static void RunAll() => Run(Keys);

    [MenuItem("Tools/몬스터/전투 측정 (보스만)")]
    private static void RunBosses() => Run(new[] { "Devil", "CyclopsBatMage", "Treeguard" });

    private static void Run(string[] keys)
    {
        if (!EditorApplication.isPlaying || s_running) return;
        RunAsync(keys).Forget();
    }

    private static async UniTaskVoid RunAsync(string[] keys)
    {
        s_running = true;
        var report = new StringBuilder();
        report.AppendLine($"# 몬스터 전투 측정 {System.DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine("키 | HP/방어 | 관찰(초) | 받은 피해 총합 | 초당 | 1회 최대 | 피격 횟수 | 검 타수(시간) | 쓴 행동");

        try
        {
            // 기준: 몬스터 없이 받는 환경 피해(배고픔·체온 등). 각 몬스터 결과에서 빼서 본다
            s_envDps = await MeasureEnvironmentAsync(10f);
            report.AppendLine($"환경 피해 기준: 초당 {s_envDps:0.00} (아래 '초당'은 이 값을 뺀 값)");

            foreach (var key in keys)
            {
                string line = await ProbeAsync(key);
                Debug.Log("[Probe] " + line);
                report.AppendLine(line);
            }
        }
        finally
        {
            s_running = false;
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"[Probe] 완료 → {ReportPath}\n{report}");
        }
    }

    private static float s_envDps;

    private static async UniTask<float> MeasureEnvironmentAsync(float seconds)
    {
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null) return 0f;
        if (!player.IsAlive) player.Revive();
        ClearMonsters();
        float total = 0f, elapsed = 0f;
        while (elapsed < seconds)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            elapsed += Time.deltaTime;
            PinPlayer(player);
            var s = player.Stat;
            if (s == null) continue;
            float lost = s.MaxHp - s.CurrentHp;
            if (lost > 0.01f) { total += lost; s.RestoreHp(lost); }
        }
        return total / seconds;
    }

    private static async UniTask<string> ProbeAsync(string key)
    {
        var player = Object.FindAnyObjectByType<Player>();
        if (player == null) return $"{key} | 플레이어 없음";
        if (!player.IsAlive) player.Revive();

        ClearMonsters();
        PinPlayer(player);

        var catalog = MonsterCatalog.Instance;
        byte id = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
        var stat = id != 0 ? catalog.GetEntry(id).statData : null;
        var monster = await Monster.SpawnAsync(key, stat, ArenaPos + new Vector3(0f, 0f, 8f));
        if (monster == null) return $"{key} | 스폰 실패";
        monster.transform.rotation = Quaternion.LookRotation(Vector3.back);

        // ① 공격 측정
        float observe = BossKeys.Contains(key) ? BossObserveSeconds : ObserveSeconds;
        float total = 0f, maxHit = 0f;
        int hits = 0;
        var anims = new HashSet<MonsterAnimId>();
        float elapsed = 0f;

        while (elapsed < observe)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            elapsed += Time.deltaTime;
            PinPlayer(player);
            if (monster == null || !monster.isActiveAndEnabled) break;
            anims.Add(monster.CurrentAnimId);

            var s = player.Stat;
            if (s == null) continue;
            if (!player.IsAlive) { player.Revive(); continue; }
            float lost = s.MaxHp - s.CurrentHp;
            // 프레임당 0.5 미만은 배고픔·체온 같은 환경 피해(연속 감소)라 세지 않는다. 몬스터 피해와 도트 틱은 1 이상이다
            if (lost >= 0.5f)
            {
                total += lost;
                hits++;
                if (lost > maxHit) maxHit = lost;
                s.RestoreHp(lost);
            }
            else if (lost > 0f)
            {
                s.RestoreHp(lost);
            }
        }

        // ② 처치 측정: 0.1초마다 검 1타. 무적 등으로 막힌 타격은 세지 않는다
        int swings = 0;
        float killTime = 0f;
        var ctx = new DamageContext(player.gameObject, monster.transform.position + Vector3.up, SwordDamage, "Weapon_Sword", ActionType.Attack);
        while (monster != null && monster.isActiveAndEnabled && monster.Status != null && !monster.Status.IsDead && killTime < 120f)
        {
            await UniTask.Delay(100, DelayType.DeltaTime);
            killTime += 0.1f;
            PinPlayer(player);
            if (player.Stat != null) player.Stat.RestoreHp(9999f);
            if (!monster.CanDamage(ctx)) continue;
            monster.ApplyDamage(ctx);
            swings++;
        }

        bool killed = monster == null || monster.Status == null || monster.Status.IsDead;
        await UniTask.Delay(500, DelayType.DeltaTime);
        ClearMonsters();

        var data = stat != null ? stat : null;
        string hpInfo = data != null ? $"{data.MaxHP}/{data.Defense}" : "?";
        float swordTime = swings * SwordSwingSeconds;
        var animList = new List<string>();
        foreach (var a in anims) animList.Add(a.ToString());
        return $"{key} | {hpInfo} | {elapsed:0} | {total:0} | {Mathf.Max(total / Mathf.Max(elapsed, 1f) - s_envDps, 0f):0.0} | {maxHit:0} | {hits} | " +
               $"{(killed ? $"{swings}타 ({swordTime:0}초)" : $"못 잡음({swings}타)")} | {string.Join(",", animList)}";
    }

    // 밀려났을 때만 제자리로 돌린다. 매 프레임 CharacterController를 껐다 켜면
    // 몬스터의 OverlapSphere 판정 순간에 플레이어 콜라이더가 물리 씬에서 빠져 있어 피해가 0으로 잡힌다.
    private static void PinPlayer(Player player)
    {
        Vector3 d = player.transform.position - ArenaPos;
        d.y = 0f;
        if (d.sqrMagnitude < 0.25f) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(ArenaPos, Quaternion.identity);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
    }

    // 측정 구역 근처의 몬스터(소환 미니언 포함)를 모두 치운다
    private static void ClearMonsters()
    {
        foreach (var m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            if ((m.transform.position - ArenaPos).sqrMagnitude > 80f * 80f) continue;
            Extensions.Despawn(m.gameObject);
        }
    }
}

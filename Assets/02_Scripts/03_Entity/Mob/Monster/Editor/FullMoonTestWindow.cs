using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 만월 보스 / 월문 테스트 창 (플레이 모드 전용). 메뉴: Tools/몬스터/만월 보스 테스트
///
/// - 만월 #1~#9: 그 만월 날 밤(22시)으로 시간을 옮기고 그 회차의 보스를 바로 예약한다.
///   실제 게임과 같은 경로(WorldMonsterSpawnDirector.UpdateBoss)로 플레이어 25~40m 밖에 생성되고, 회차 배율도 그대로 걸린다.
///   같은 회차를 다시 누르면 지금 보스를 치우고 다시 나온다.
/// - 플레이어: 회차에 맞는 무기 세트 지급, 음식 지급, 회복, 무적
/// - 보스: 보스 앞으로 이동, 체력 10%, 즉시 처치 (드롭·사망 흐름 확인용)
/// - 월문: 지급/바로 설치/이동/도전 기록 초기화 → 만월 날 E키로 입장해 최종 보스 흐름을 확인한다
///
/// 싱글 또는 멀티 호스트에서 쓴다 (시간과 보스 판정이 호스트에 있다).
/// </summary>
public class FullMoonTestWindow : EditorWindow
{
    private const string RulePath = "Assets/05_Datas/WorldData_KGB/SpawnRuleSets/Boss/FullMoonBossCycle.asset";
    private const string GearFolder = "Assets/05_Datas/ItemData_JYJ/Item_CombatGears/";
    private const string DishFolder = "Assets/05_Datas/ItemData_JYJ/Item_Dish/";
    private const string MoonGateItemPath = "Assets/05_Datas/ItemData_JYJ/Item_SurvivalTools/Tool_MoonGate.asset";

    // 보스 생성 시각: 22시 (하루 1440초 중 1320초)
    private const float NightSeconds = WorldClock.SecondsPerDay * 22f / 24f;

    private static readonly string[] BasicGear =
        { "Armor_Helmet", "Armor_Chestplate", "Armor_Shield", "Weapon_Spear", "Weapon_Bow", "Weapon_Sword" };
    private static readonly string[] MagicGear =
        { "Armor_Helmet", "Armor_Chestplate", "Armor_MagicShield", "Weapon_MagicSpear", "Weapon_MagicBow",
          "Weapon_MagicWand", "Weapon_MagicSword_Cyan" };
    private static readonly string[] Dishes =
        { "Dish_MeatStew", "Dish_FishSteak", "Dish_BerryPie", "Dish_MushroomSoup", "Dish_Omelette" };

    private static readonly string[] BossNames =
    {
        "Devil:임프 데빌", "Treeguard:트리가드", "CyclopsBatMage:외눈 박쥐 메이지",
    };

    private bool autoGear = true;
    private Vector2 scroll;

    [MenuItem("Tools/몬스터/만월 보스 테스트")]
    private static void Open() => GetWindow<FullMoonTestWindow>("만월 보스 테스트");

    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        var rule = AssetDatabase.LoadAssetAtPath<BossSpawnRule>(RulePath);
        if (rule == null)
        {
            EditorGUILayout.HelpBox("BossSpawnRule을 찾을 수 없습니다: " + RulePath, MessageType.Error);
            return;
        }

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("플레이 모드의 GameScene에서 사용합니다. (싱글 또는 멀티 호스트)", MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawStatus();

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            DrawFullMoons(rule);
            DrawBoss();
            DrawPlayer();
            DrawMoonGate();
        }

        EditorGUILayout.EndScrollView();
    }

    #region 상태

    private void DrawStatus()
    {
        if (!EditorApplication.isPlaying) return;

        WorldClock clock = WorldClock.Instance;
        string time = clock != null
            ? $"{clock.CurrentDay}일차 {clock.CurrentHour:00}시  {clock.CurrentTimePhase}  달: {clock.CurrentMoonPhase}"
            : "WorldClock 없음 (GameScene이 아님)";
        EditorGUILayout.LabelField("시간", time);

        Monster boss = FindBoss();
        string bossText = boss != null && boss.Status != null
            ? $"{boss.name}  HP {boss.Status.CurrentHp:0}/{boss.Status.MaxHp:0}  배율 ×{boss.StatScale:0.##}  " +
              $"거리 {DistanceToPlayer(boss):0}m"
            : "없음";
        EditorGUILayout.LabelField("보스", bossText);

        MoonGate gate = MoonGate.Instance;
        EditorGUILayout.LabelField("월문", gate != null ? gate.State.ToString() : "설치 안 됨");

#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom && !Main.Network.IsHost)
            EditorGUILayout.HelpBox("멀티 클라이언트에서는 시간·보스를 바꿀 수 없습니다. 호스트에서 쓰세요.", MessageType.Warning);
#endif
        EditorGUILayout.Space();
    }

    #endregion

    #region 만월 #1~#9

    private void DrawFullMoons(BossSpawnRule rule)
    {
        EditorGUILayout.LabelField("만월 보스 (그 만월 날 22시로 이동 + 보스 예약)", EditorStyles.boldLabel);
        autoGear = EditorGUILayout.Toggle("회차에 맞는 무기 자동 지급", autoGear);

        for (int row = 0; row < 3; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < 3; col++)
            {
                int number = row * 3 + col + 1;
                int day = FullMoonDay(number);
                string key = rule.GetMonsterKey(day);
                string label = $"#{number}  {day}일차\n{BossLabel(key)}  ×{rule.GetStatMultiplier(day):0.##}";
                if (GUILayout.Button(label, GUILayout.Height(40)))
                    StartFullMoon(number);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
    }

    /// <summary>n번째 만월의 표시 일차 (1일차부터 세어 5, 13, 21, ...).</summary>
    private static int FullMoonDay(int number) => (int)MoonPhase.Full + 1 + (number - 1) * WorldClock.MoonCycleDays;

    private static string BossLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return "(없음)";
        foreach (string entry in BossNames)
        {
            int split = entry.IndexOf(':');
            if (entry.Substring(0, split) == key) return entry.Substring(split + 1);
        }
        return key;
    }

    private void StartFullMoon(int number)
    {
        int day = FullMoonDay(number);
        if (!SetNight(day)) return;

        var director = Object.FindAnyObjectByType<WorldMonsterSpawnDirector>();
        if (director == null)
        {
            Debug.LogWarning("[만월 테스트] WorldMonsterSpawnDirector가 없습니다.");
            return;
        }
        director.DebugForceFullMoon(day);

        if (autoGear) GiveGear(number <= 3 ? BasicGear : MagicGear);
        Debug.Log($"[만월 테스트] #{number} 만월 ({day}일차 22시) — 보스가 플레이어 25~40m 밖에 곧 생성됩니다.");
    }

    // 표시 일차 day의 22시로 맞춘다. 22시는 자정 이전이라 자정 기준 경과 일수 = day - 1.
    private static bool SetNight(int day)
    {
        WorldClock clock = WorldClock.Instance;
        if (clock == null)
        {
            Debug.LogWarning("[만월 테스트] WorldClock이 없습니다. GameScene에서 쓰세요.");
            return false;
        }
        clock.SyncTime(day - 1, NightSeconds);
        return true;
    }

    #endregion

    #region 보스

    private void DrawBoss()
    {
        EditorGUILayout.LabelField("보스", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("보스 앞으로 이동")) MoveToBoss();
        if (GUILayout.Button("보스 체력 10%")) SetBossHp(0.1f);
        if (GUILayout.Button("보스 즉시 처치")) KillBoss();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 월문 아레나 안이면 가장 가까운 드래곤, 밖이면 만월 보스. 둘 다 없으면 60m 안의 가장 가까운 몬스터
    private static Monster FindBoss()
    {
        Player player = FindLocalPlayer();
        bool inArena = player != null && player.InBossArena;

        var director = Object.FindAnyObjectByType<WorldMonsterSpawnDirector>();
        Monster boss = director != null && !inArena ? director.DebugCurrentBoss : null;
        if (boss != null) return boss;

        Vector3 center = player != null ? player.transform.position : Vector3.zero;
        Monster nearestDragon = null, nearest = null;
        float dragonDist = float.MaxValue, nearDist = 60f * 60f;
        foreach (var m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m == null || !m.isActiveAndEnabled || m.Status == null || m.Status.IsDead) continue;
            float d = (m.transform.position - center).sqrMagnitude;
            if (m is DragonBoss && d < dragonDist) { dragonDist = d; nearestDragon = m; }
            if (d < nearDist) { nearDist = d; nearest = m; }
        }
        return nearestDragon != null ? nearestDragon : nearest;
    }

    private static float DistanceToPlayer(Monster monster)
    {
        Player player = FindLocalPlayer();
        return player != null ? Vector3.Distance(player.transform.position, monster.transform.position) : 0f;
    }

    private static void MoveToBoss()
    {
        Player player = FindLocalPlayer();
        Monster boss = FindBoss();
        if (player == null || boss == null)
        {
            Debug.LogWarning("[만월 테스트] 플레이어 또는 보스가 없습니다. (보스는 만월 버튼 뒤 잠시 후 생성)");
            return;
        }

        Vector3 away = player.transform.position - boss.transform.position;
        away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized;
        Vector3 pos = SnapToGround(boss.transform.position + away * 10f, boss.transform.position.y) + Vector3.up * 0.5f;
        player.Motor?.Teleport(pos);
    }

    private static void SetBossHp(float ratio)
    {
        Monster boss = FindBoss();
        if (boss == null || boss.Status == null) return;
        boss.Status.SetCurrentHp(boss.Status.MaxHp * ratio);
    }

    // 실제 사망 경로(OnDead → 드롭·디렉터 사망 처리)를 그대로 타도록 큰 피해로 죽인다
    private static void KillBoss()
    {
        Monster boss = FindBoss();
        if (boss == null || boss.Status == null) return;
        boss.Status.TakeDamage(boss.Status.MaxHp * 100f + 100000f);
    }

    #endregion

    #region 플레이어

    private void DrawPlayer()
    {
        EditorGUILayout.LabelField("플레이어", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("기본 무기 세트")) GiveGear(BasicGear);
        if (GUILayout.Button("마력 무기 세트")) GiveGear(MagicGear);
        if (GUILayout.Button("음식 ×5")) GiveDishes();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("체력·허기·정신력 회복")) Heal();
        Player player = FindLocalPlayer();
        bool invincible = player != null && player.Stat != null && player.Stat.Invincible;
        bool next = GUILayout.Toggle(invincible, "무적", "Button");
        if (player != null && player.Stat != null && next != invincible) player.Stat.Invincible = next;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    // 방어구를 먼저 끼고, 목록의 마지막 무기를 손에 든다. 나머지 무기는 인벤토리에 남는다.
    // 이미 가진(인벤토리에 있거나 장착 중인) 장비는 다시 넣지 않아서, 회차를 여러 번 눌러도 인벤토리가 차지 않는다.
    private static void GiveGear(string[] names)
    {
        Player player = FindLocalPlayer();
        PlayerInventory inventory = player != null ? player.Inventory : null;
        if (inventory == null) return;

        ItemDataSO hand = null;
        foreach (string name in names)
        {
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(GearFolder + name + ".asset");
            if (so == null)
            {
                Debug.LogWarning($"[만월 테스트] 아이템 없음: {name}");
                continue;
            }

            bool owned = inventory.GetItemCount(so) > 0 || inventory.GetEquippedItem(so.equipSlot) == so;
            if (!owned && !inventory.AddItem(so, 1))
            {
                Debug.LogWarning($"[만월 테스트] 인벤토리가 가득 차 {name}을(를) 넣지 못했습니다.");
                continue;
            }

            if (so.equipSlot == EquipSlot.Hand) hand = so;
            else if (inventory.GetEquippedItem(so.equipSlot) != so) inventory.EquipItem(so);
        }
        if (hand != null && inventory.GetEquippedItem(EquipSlot.Hand) != hand) inventory.EquipItem(hand);
    }

    private static void GiveDishes()
    {
        Player player = FindLocalPlayer();
        if (player == null || player.Inventory == null) return;
        foreach (string name in Dishes)
        {
            var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(DishFolder + name + ".asset");
            if (so != null) player.Inventory.AddItem(so, 5);
        }
    }

    private static void Heal()
    {
        Player player = FindLocalPlayer();
        if (player == null || player.Stat == null) return;
        if (player.IsDead)
        {
            player.Revive();
            return;
        }
        player.Stat.RestoreHp(player.Stat.MaxHp);
        player.Stat.RestoreHunger(player.Stat.MaxHunger);
        player.Stat.RestoreEgo(player.Stat.MaxEgo);
    }

    #endregion

    #region 월문

    private void DrawMoonGate()
    {
        EditorGUILayout.LabelField("월문 → 최종 보스", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("월문은 만월 날 하루 동안 열립니다. 만월 버튼으로 날짜를 옮긴 뒤 월문 앞에서 E키로 입장하세요.", MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("월문 아이템 지급")) GiveMoonGateItem();
        if (GUILayout.Button("앞에 바로 설치 (8m)")) PlaceMoonGate();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("월문 앞으로 이동")) MoveToMoonGate();
        if (GUILayout.Button("오늘 도전 기록 초기화")) ResetMoonGateAttempt();
        EditorGUILayout.EndHorizontal();
    }

    private static void GiveMoonGateItem()
    {
        Player player = FindLocalPlayer();
        var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(MoonGateItemPath);
        if (player != null && player.Inventory != null && so != null) player.Inventory.AddItem(so, 1);
    }

    private static void PlaceMoonGate()
    {
        Player player = FindLocalPlayer();
        var so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(MoonGateItemPath);
        if (player == null || so == null) return;
        if (MoonGate.Instance != null)
        {
            Debug.LogWarning("[만월 테스트] 월문이 이미 설치돼 있습니다. '월문 앞으로 이동'을 쓰세요.");
            return;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        Vector3 pos = SnapToGround(player.transform.position + forward * 8f, player.transform.position.y);
        StructureSync.Place(so, pos, Quaternion.LookRotation(-forward));
    }

    private static void MoveToMoonGate()
    {
        Player player = FindLocalPlayer();
        MoonGate gate = MoonGate.Instance;
        if (player == null || gate == null)
        {
            Debug.LogWarning("[만월 테스트] 월문이 설치돼 있지 않습니다.");
            return;
        }
        Vector3 pos = gate.transform.position + gate.transform.forward * 4f;
        player.Motor?.Teleport(SnapToGround(pos, gate.transform.position.y) + Vector3.up * 0.5f);
    }

    // 한 번 도전한 날은 다시 열리지 않으므로 같은 날 재도전하려면 기록을 지운다
    private static void ResetMoonGateAttempt()
    {
        MoonGate gate = MoonGate.Instance;
        FieldInfo field = typeof(MoonGate).GetField("lastAttemptDay", BindingFlags.Instance | BindingFlags.NonPublic);
        if (gate == null || field == null) return;
        field.SetValue(gate, -1);
        Debug.Log("[만월 테스트] 월문 도전 기록을 초기화했습니다.");
    }

    #endregion

    #region 공통

    private static Vector3 SnapToGround(Vector3 pos, float fallbackY)
    {
        if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Monster>() == null && hit.collider.GetComponentInParent<Player>() == null)
            return hit.point;
        pos.y = fallbackY;
        return pos;
    }

    private static Player FindLocalPlayer()
    {
        Player any = null;
        foreach (var p in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.IsLocalPlayer) return p;
            if (any == null) any = p;
        }
        return any;
    }

    #endregion
}

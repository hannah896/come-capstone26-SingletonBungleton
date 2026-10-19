using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 몬스터 소환기 (플레이 모드 테스트용). 메뉴: Tools/몬스터/몬스터 소환기
///
/// MonsterCatalog에 등록된 몬스터를 종류별 버튼으로 보여주고, 누르면 로컬 플레이어 앞에 소환한다.
/// - 거리·마릿수를 정할 수 있고, 여러 마리는 플레이어 앞 부채꼴로 나란히 세운다
/// - 소환된 몬스터는 플레이어를 바라본다. 바닥은 아래로 레이캐스트해서 맞춘다
/// - 드래곤은 "등장 연출"을 켜면 이륙 → 포효 → 착지로 나온다. 보스전(서리룡 → 망령룡) 시작 버튼도 있다
/// - 멀티 호스트에서 소환하면 복제 대상으로 등록된다
/// </summary>
public class MonsterSpawnerWindow : EditorWindow
{
    private const string CatalogPath = "Assets/Resources/ConfigData/MonsterCatalog.asset";

    // 버튼에 보일 이름과 분류. 카탈로그에 새 몬스터가 생기면 "기타"로 나온다
    private static readonly Dictionary<string, (string label, string group)> Info = new Dictionary<string, (string, string)>
    {
        { "Mischief", ("미스치프", "일반") },
        { "Demon", ("데몬", "일반") },
        { "TreantMinionAutumn", ("트리앤트 미니언 (가을)", "일반") },
        { "TreantMinionEvergreen", ("트리앤트 미니언 (상록)", "일반") },
        { "TreantTreeAutumn", ("트리앤트 트리 (가을)", "일반") },
        { "TreantTreeEvergreen", ("트리앤트 트리 (상록)", "일반") },
        { "CyclopsBat", ("외눈 박쥐", "일반") },
        { "CyclopsBatWizard", ("외눈 박쥐 위저드", "정예") },
        { "Devil", ("임프 데빌", "만월 보스") },
        { "Treeguard", ("트리가드", "만월 보스") },
        { "TreeguardAutumn", ("트리가드 (가을)", "만월 보스") },
        { "CyclopsBatMage", ("외눈 박쥐 메이지", "만월 보스") },
        { "PolarDragon", ("서리룡 (1페이즈)", "최종 보스") },
        { "UndeadDragon", ("망령룡 (2페이즈)", "최종 보스") },
    };

    private static readonly string[] GroupOrder = { "일반", "정예", "만월 보스", "최종 보스", "기타" };

    private float distance = 8f;
    private int count = 1;
    private bool dragonIntro = true;
    private Vector2 scroll;

    [MenuItem("Tools/몬스터/몬스터 소환기")]
    private static void Open() => GetWindow<MonsterSpawnerWindow>("몬스터 소환기");

    private void OnGUI()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("MonsterCatalog를 찾을 수 없습니다: " + CatalogPath, MessageType.Error);
            return;
        }

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("플레이 모드에서만 소환할 수 있습니다.", MessageType.Info);

        distance = EditorGUILayout.Slider("플레이어 앞 거리(m)", distance, 2f, 40f);
        count = EditorGUILayout.IntSlider("마릿수", count, 1, 10);
        dragonIntro = EditorGUILayout.Toggle("드래곤 등장 연출", dragonIntro);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("주변 몬스터 모두 제거 (50m)")) ClearNearby(50f);
            if (GUILayout.Button("보스전 시작 (서리룡 → 망령룡)")) StartEncounter();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // 분류별로 묶어서 버튼을 그린다 (CatalogId 순서는 유지)
            foreach (var group in GroupOrder)
            {
                bool header = false;
                foreach (var entry in catalog.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.addressableKey)) continue;
                    string g = Info.TryGetValue(entry.addressableKey, out var info) ? info.group : "기타";
                    if (g != group) continue;

                    if (!header)
                    {
                        EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                        header = true;
                    }

                    string label = Info.TryGetValue(entry.addressableKey, out info) ? info.label : entry.addressableKey;
                    string hp = entry.statData != null ? $"  HP {entry.statData.MaxHP:0}" : "";
                    if (GUILayout.Button($"{label}  ({entry.addressableKey}){hp}"))
                        Spawn(catalog, entry.addressableKey);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void Spawn(MonsterCatalog catalog, string key)
    {
        var player = FindLocalPlayer();
        if (player == null)
        {
            Debug.LogWarning("[몬스터 소환기] 플레이어가 없습니다.");
            return;
        }

        byte catalogId = catalog.GetCatalogId(key);
        var stat = catalogId != 0 ? catalog.GetEntry(catalogId).statData : null;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

        // 여러 마리는 플레이어 앞 부채꼴(최대 ±40°)로 나란히
        for (int i = 0; i < count; i++)
        {
            float angle = count <= 1 ? 0f : Mathf.Lerp(-40f, 40f, i / (float)(count - 1));
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 pos = SnapToGround(player.transform.position + dir * distance, player.transform.position.y);
            SpawnAsync(key, stat, catalogId, pos, player.transform.position, dragonIntro).Forget();
        }
    }

    private static async UniTaskVoid SpawnAsync(string key, MonsterStatData stat, byte catalogId, Vector3 pos, Vector3 lookAt,
                                                bool dragonIntro)
    {
        var monster = await Monster.SpawnAsync(key, stat, pos);
        if (monster == null)
        {
            Debug.LogWarning($"[몬스터 소환기] '{key}' 소환 실패");
            return;
        }

        Vector3 look = lookAt - pos;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) monster.transform.rotation = Quaternion.LookRotation(look);

        if (monster is DragonBoss dragon)
        {
            dragon.SetArenaCenter(pos);
            if (dragonIntro) dragon.BeginIntro();
        }

#if PHOTON_FUSION
        if (catalogId != 0 && Main.Network != null && Main.Network.IsInRoom)
            NetworkMonsterDirector.Instance?.RegisterMonster(monster, catalogId);
#endif
        Debug.Log($"[몬스터 소환기] {key} 소환 {pos}");
    }

    private void StartEncounter()
    {
        var player = FindLocalPlayer();
        if (player == null) return;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

        var go = new GameObject("FinalBossEncounter (소환기)");
        go.transform.position = SnapToGround(player.transform.position + forward * Mathf.Max(distance, 15f), player.transform.position.y);
        go.transform.rotation = Quaternion.LookRotation(-forward);
        go.AddComponent<FinalBossEncounter>().StartEncounter();
    }

    private static void ClearNearby(float radius)
    {
        var player = FindLocalPlayer();
        Vector3 center = player != null ? player.transform.position : Vector3.zero;
        int removed = 0;
        foreach (var m in Object.FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            if ((m.transform.position - center).sqrMagnitude > radius * radius) continue;
            Extensions.Despawn(m.gameObject);
            removed++;
        }
        DragonHazardZone.ClearAll();
        Debug.Log($"[몬스터 소환기] 몬스터 {removed}마리 제거");
    }

    // 지형 위에 세운다. 바닥을 못 찾으면 플레이어 발 높이를 쓴다
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
}

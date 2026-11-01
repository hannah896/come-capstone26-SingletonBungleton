using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 월드(GameScene)에서 가축(닭, 사슴)을 플레이어 주변에 유지하는 스포너.
///
/// 지금까지 동물은 테스트 씬에만 배치돼 있어서 실제 게임 월드에는 하나도 없었다.
/// 이 스포너가 플레이어 주변 링(최소~최대 거리) 안의 NavMesh 위에 종류별 목표 마리 수까지 채우고,
/// 너무 멀어진 동물은 풀로 돌려보낸다. 싱글/호스트만 스폰하고(클라이언트는 NetworkAnimalDirector 복제),
/// GameScene에서만 동작한다.
///
/// 시작 시 자동으로 생성된다 (RuntimeInitializeOnLoadMethod). 수치는 Inspector가 아니라 아래 상수로 조정한다.
/// </summary>
public sealed class WorldAnimalSpawner : MonoBehaviour
{
    private sealed class Rule
    {
        public string Key;
        public int TargetCount;
    }

    // 종류별 목표 마리 수 (임시값)
    private static readonly Rule[] Rules =
    {
        new Rule { Key = "Chicken", TargetCount = 6 },
        new Rule { Key = "Deer", TargetCount = 4 },
    };

    private const float MinSpawnDistance = 35f;
    private const float MaxSpawnDistance = 65f;
    private const float DespawnDistance = 140f;
    private const float CheckInterval = 2f;
    private const int PositionTries = 8;
    // 사냥으로 죽은 동물은 바로 채우지 않고, 이만큼 지난 뒤에 다른 자리에서 새로 나온다 (초, 임시값)
    private const float RespawnDelay = 90f;

    private readonly List<(Animal animal, string key)> spawned = new();
    private readonly List<(string key, float readyTime)> respawnQueue = new();
    private float timer;
    private bool spawning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<WorldAnimalSpawner>() != null) return;

        var go = new GameObject("WorldAnimalSpawner");
        DontDestroyOnLoad(go);
        go.AddComponent<WorldAnimalSpawner>();
    }

    private void Update()
    {
        if (!IsActiveInThisScene()) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = CheckInterval;

        Prune();

        List<Player> players = GetAlivePlayers();
        if (players.Count == 0) return;

        DespawnFarAnimals(players);
        if (spawning) return;

        foreach (Rule rule in Rules)
        {
            if (CountOf(rule.Key) >= rule.TargetCount) continue;

            SpawnOneAsync(rule.Key, players).Forget();
            break;   // 한 번에 한 마리씩 (로드가 겹치지 않게)
        }
    }

    // GameScene에서, 게임 로직이 도는 동안, 호스트(또는 싱글)만 동작한다
    private static bool IsActiveInThisScene()
    {
        if (Main.Instance == null || Main.Scene == null) return false;
        if (!(Main.Scene.Current is GameScene)) return false;
        if (!GameScene.IsGameUpdating) return false;
        return Animal.IsSimulatedPeer;
    }

    private static List<Player> GetAlivePlayers()
    {
        var result = new List<Player>();
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (player != null && player.IsAlive) result.Add(player);

        return result;
    }

    // 죽어서(또는 풀로 돌아가) 사라진 동물은 목록에서 빼고, 일정 시간 뒤에 다른 자리에서 새로 나오도록 예약한다
    private void Prune()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            Animal a = spawned[i].animal;
            if (a != null && a.isActiveAndEnabled) continue;

            respawnQueue.Add((spawned[i].key, Time.time + RespawnDelay));
            spawned.RemoveAt(i);
        }

        // 예약 시간이 지난 것은 큐에서 빼서, 아래 CountOf가 부족분으로 계산하게 한다
        for (int i = respawnQueue.Count - 1; i >= 0; i--)
            if (respawnQueue[i].readyTime <= Time.time) respawnQueue.RemoveAt(i);
    }

    // 살아있는 수 + 아직 되살아날 시간이 안 된 예약 수
    private int CountOf(string key)
    {
        int count = 0;
        foreach ((Animal _, string k) in spawned)
            if (k == key) count++;

        foreach ((string k, float _) in respawnQueue)
            if (k == key) count++;

        return count;
    }

    // 모든 플레이어에게서 멀어진 동물은 풀로 되돌린다 (먼 곳에 쌓이지 않게)
    private void DespawnFarAnimals(List<Player> players)
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            Animal a = spawned[i].animal;
            if (a == null) { spawned.RemoveAt(i); continue; }

            float nearest = float.MaxValue;
            foreach (Player p in players)
                nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, a.transform.position));

            if (nearest <= DespawnDistance) continue;

            spawned.RemoveAt(i);
            Extensions.Despawn(a.gameObject);
        }
    }

    private async UniTaskVoid SpawnOneAsync(string key, List<Player> players)
    {
        spawning = true;
        try
        {
            AnimalCatalog catalog = AnimalCatalog.Instance;
            byte catalogId = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
            if (catalogId == 0) return;

            if (!TryFindSpawnPoint(players, out Vector3 point)) return;

            AnimalCatalog.Entry entry = catalog.GetEntry(catalogId);
            Animal animal = await Animal.SpawnAsync(entry.addressableKey, entry.statData, point);
            if (animal == null) return;

            spawned.Add((animal, key));

#if PHOTON_FUSION
            NetworkAnimalDirector.Instance?.RegisterAnimal(animal, catalogId);
#endif
        }
        finally
        {
            spawning = false;
        }
    }

    // 무작위 플레이어 주변 링 안에서, 지면과 NavMesh가 모두 있는 지점을 찾는다
    private static bool TryFindSpawnPoint(List<Player> players, out Vector3 point)
    {
        point = default;
        Player target = players[Random.Range(0, players.Count)];

        for (int i = 0; i < PositionTries; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(MinSpawnDistance, MaxSpawnDistance);
            Vector3 candidate = target.transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

            if (!Physics.Raycast(candidate + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f,
                    ~0, QueryTriggerInteraction.Ignore))
                continue;

            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                continue;

            point = navHit.position;
            return true;
        }

        return false;
    }
}

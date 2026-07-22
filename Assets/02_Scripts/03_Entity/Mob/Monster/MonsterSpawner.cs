using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 몬스터 스포너. 씬에 배치해 주기적으로 몬스터를 스폰한다.
///
/// 멀티플레이에서는 호스트만 스폰한다(Monster.IsSimulatedPeer). 클라이언트의 몬스터는
/// NetworkMonsterDirector가 복제 정보를 받아 로컬로 만들어 주므로 여기서 만들면 중복된다.
/// 싱글플레이(방 미참가)는 IsSimulatedPeer가 true라 그대로 동작한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("스폰 대상")]
    [Tooltip("MonsterCatalog에 등록된 몬스터의 Addressable 키")]
    [SerializeField] private string monsterKey = "";

    [Header("스폰 규칙")]
    [Tooltip("이 스포너가 동시에 유지할 몬스터 수")]
    [SerializeField] private int maxAlive = 3;
    [Tooltip("스폰 간격(초)")]
    [SerializeField] private float spawnInterval = 10f;
    [Tooltip("스포너 위치 기준 스폰 반경(m). 0이면 정확히 스포너 위치에 스폰")]
    [SerializeField] private float spawnRadius = 5f;
    [Tooltip("시작하자마자 maxAlive만큼 즉시 채울지")]
    [SerializeField] private bool fillOnStart = true;

    private readonly System.Collections.Generic.List<Monster> _alive = new();
    private float _cooldown;
    private bool _spawning;

    private void OnEnable()
    {
        Main.Loop.OnGameUpdate += OnGameUpdate;
        _cooldown = fillOnStart ? 0f : spawnInterval;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= OnGameUpdate;
    }

    private void OnGameUpdate(float deltaTime)
    {
        // 클라이언트는 스폰하지 않는다 — 디렉터가 호스트 몬스터를 복제해 준다.
        if (!Monster.IsSimulatedPeer) return;
        if (string.IsNullOrEmpty(monsterKey)) return;

        PruneDead();

        if (_alive.Count >= maxAlive) return;

        _cooldown -= deltaTime;
        if (_cooldown > 0f) return;

        _cooldown = spawnInterval;
        SpawnOneAsync().Forget();
    }

    // 죽어서 풀로 돌아간 몬스터를 목록에서 걷어낸다.
    private void PruneDead()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            Monster m = _alive[i];
            if (m == null || !m.isActiveAndEnabled)
                _alive.RemoveAt(i);
        }
    }

    private async UniTaskVoid SpawnOneAsync()
    {
        if (_spawning) return; // 로드 대기 중 중복 스폰 방지
        _spawning = true;

        try
        {
            MonsterCatalog catalog = MonsterCatalog.Instance;
            byte catalogId = catalog.GetCatalogId(monsterKey);
            if (catalogId == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[MonsterSpawner] '{monsterKey}'가 MonsterCatalog에 없습니다. " +
                               "Resources/ConfigData/MonsterCatalog.asset에 추가하세요.", this);
#endif
                return;
            }

            MonsterCatalog.Entry entry = catalog.GetEntry(catalogId);
            Monster monster = await Monster.SpawnAsync(entry.addressableKey, entry.statData, GetSpawnPosition());
            if (monster == null) return;

            _alive.Add(monster);

#if PHOTON_FUSION
            // 멀티플레이면 복제 대상으로 등록한다. 싱글이면 디렉터가 없어 그대로 로컬 몬스터로 남는다.
            NetworkMonsterDirector.Instance?.RegisterMonster(monster, catalogId);
#endif
        }
        finally
        {
            _spawning = false;
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnRadius <= 0f) return transform.position;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}

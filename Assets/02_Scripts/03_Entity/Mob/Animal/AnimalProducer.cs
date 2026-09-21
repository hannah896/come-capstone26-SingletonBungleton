using UnityEngine;

/// <summary>
/// 동물이 일정 주기로 아이템을 생산해 발밑에 떨어뜨린다. (닭 → 알, 양 → 양털 등)
///
/// 호스트(또는 싱글)만 생산한다. 떨어뜨리는 건 <see cref="WorldItemSync.SpawnDroppedItem"/>이라
/// 멀티에서는 호스트를 거쳐 모든 피어에 같은 아이템이 생긴다. 클라가 따로 만들면 아이템이 중복된다.
///
/// 주변에 같은 아이템이 이미 많이 쌓여 있으면 생산을 건너뛴다 (방치 시 월드가 아이템으로 덮이는 것 방지).
/// </summary>
[RequireComponent(typeof(Animal))]
public class AnimalProducer : MonoBehaviour
{
    [Header("생산")]
    [Tooltip("떨어뜨릴 아이템의 Addressable 키 (= ItemDataSO 파일명)")]
    [SerializeField] private string itemKey = "Booty_Egg";

    [Tooltip("한 번에 떨어뜨리는 개수")]
    [SerializeField] private int countPerProduce = 1;

    [Tooltip("생산 주기 최소/최대(초, 게임 시간). 매번 이 사이에서 랜덤으로 고른다")]
    [SerializeField] private Vector2 intervalRange = new(90f, 150f);

    [Tooltip("몸 기준 뒤쪽으로 이만큼 떨어진 곳에 놓는다(m)")]
    [SerializeField] private float dropBackOffset = 0.3f;

    [Header("쌓임 제한")]
    [Tooltip("이 반경(m) 안에 같은 아이템이 maxNearbyCount개 이상 있으면 이번 생산을 건너뛴다")]
    [SerializeField] private float nearbyCheckRadius = 4f;
    [SerializeField] private int maxNearbyCount = 3;

    [Tooltip("주변 아이템을 찾을 레이어 (바닥 아이템 레이어)")]
    [SerializeField] private LayerMask itemMask = ~0;

    private Animal animal;
    private float timer;
    private bool loopHooked;

    private readonly Collider[] nearbyBuffer = new Collider[32];

    private void Awake()
    {
        animal = GetComponent<Animal>();
    }

    // 풀에서 다시 꺼낼 때마다 활성화되므로 여기서 주기를 새로 뽑는다.
    private void OnEnable()
    {
        ResetTimer();

        if (loopHooked) return;
        Main.Loop.OnGameUpdate += OnGameUpdate;
        loopHooked = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= OnGameUpdate;
        loopHooked = false;
    }

    private void OnGameUpdate(float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        if (!Animal.IsSimulatedPeer) return;
        if (animal == null || animal.Status == null || animal.Status.IsDead) return;

        timer -= deltaTime;
        if (timer > 0f) return;

        ResetTimer();
        TryProduce();
    }

    private void ResetTimer()
    {
        float min = Mathf.Max(1f, intervalRange.x);
        float max = Mathf.Max(min, intervalRange.y);
        timer = Random.Range(min, max);
    }

    private void TryProduce()
    {
        if (string.IsNullOrEmpty(itemKey) || countPerProduce <= 0) return;
        if (CountNearbySameItems() >= maxNearbyCount) return;

        Vector3 position = transform.position - transform.forward * dropBackOffset;
        WorldItemSync.SpawnDroppedItem(itemKey, countPerProduce, position);
    }

    // 주변 바닥에 같은 아이템이 몇 개 있는지 센다. (아이템 키 = ItemDataSO 파일명)
    private int CountNearbySameItems()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, nearbyCheckRadius, nearbyBuffer,
            itemMask, QueryTriggerInteraction.Collide);

        int count = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Item item = nearbyBuffer[i].GetComponentInParent<Item>();
            if (item == null || item.ItemDataSO == null) continue;
            if (item.ItemDataSO.name == itemKey) count++;
        }

        return count;
    }
}

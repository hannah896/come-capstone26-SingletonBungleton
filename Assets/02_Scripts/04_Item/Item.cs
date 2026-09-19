using UnityEngine;

/// <summary>
/// 월드에 존재하는 아이템 오브젝트의 베이스 컴포넌트.
/// Inspector에서 ItemDataSO를 연결하면 Awake 시점에 런타임 데이터(ItemData)를 자동 생성한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Item : MonoBehaviour, IDisposeInitializable
{
    [Header("=== 아이템 데이터 ===")]
    [Tooltip("아이템 원본 데이터 (Inspector에서 SO 연결)")]
    [SerializeField] private ItemDataSO _itemSO;

    [Header("=== 런타임 초기값 ===")]
    [Tooltip("월드에 놓인 스택형 아이템의 초기 수량")]
    [SerializeField, Min(1)] private int stackCount = 1;

    [Header("=== 횃불 ===")]
    [Tooltip("횃불 아이템일 때 제어할 Light 컴포넌트")]
    [SerializeField] private Light _torchLight;

    [Header("=== 월드 배치 ===")]
    [Tooltip("월드 생성으로 배치된 아이템을 주운 뒤 다시 생기기까지의 인게임 시간(초). 기본 1일")]
    [SerializeField, Min(0f)] private float placedRespawnSeconds = 1440f;

    [System.NonSerialized]
    public ItemData itemData;  // 런타임 전용 — Awake에서 _itemSO 기반으로 생성
    private float spoilDeadline = -1f;
    public float SpoilRemainingSeconds => spoilDeadline < 0f ? -1f : Mathf.Max(0f, spoilDeadline - SavePlayClock.Now);

    public ItemStackSaveData CaptureSaveData(int slotIndex = -1)
    {
        int count = itemData is IStackable stack ? stack.stackCount : stackCount;
        return ItemSaveCatalog.Create(ItemDataSO, count, slotIndex,
            itemData is ItemData_Equipable equipment ? equipment.CurrentDurability : -1f,
            SpoilRemainingSeconds);
    }

    public void RestoreSaveData(ItemStackSaveData saved)
    {
        if (saved == null) return;
        stackCount = Mathf.Max(1, saved.count);
        if (itemData is IStackable stack) stack.stackCount = stackCount;
        if (itemData is ItemData_Equipable equipment) equipment.RestoreDurability(saved.durability);
        spoilDeadline = saved.spoilRemainingSeconds < 0f ? -1f : SavePlayClock.Now + saved.spoilRemainingSeconds;
    }

    // 표시 오브젝트는 인벤토리가 소유하는 장비 모델을 함께 사용한다.
    public void BindRuntimeData(ItemData runtime)
    {
        if (itemData is Item_SurvivalTool oldTool) oldTool.OnTorchToggled -= SetTorchLight;
        itemData = runtime;
        _itemSO = runtime?.data;
        BindTorchLight();
    }

    // 월드 생성(ItemDisposer)으로 배치된 경우의 배치 정보 — 드롭/장착 뷰로 쓰일 때는 null
    [System.NonSerialized] private DisposeData _placement;
    [System.NonSerialized] private ChunkData _chunk;

    /// <summary>멀티플레이 바닥 아이템 ID (NetworkWorldState가 부여). 0이면 네트워크 관리 대상이 아님.</summary>
    [System.NonSerialized] public int NetworkDropId;

    // 줍기 요청을 보낸 뒤 호스트 응답을 기다리는 기한 (응답이 유실돼도 이 시각이 지나면 다시 줍기 가능)
    [System.NonSerialized] private float _pickupPendingUntil;
    private const float PickupPendingTimeout = 1.5f;

    // 프리팹에 설정된 초기 수량 (Init(so)가 stackCount를 덮어쓰므로 Awake에서 보관)
    [System.NonSerialized] private int _prefabStackCount = 1;

    public bool IsWorldPlaced => _placement != null && _placement.instanceId != 0;
    public int PlacementId => _placement != null ? _placement.instanceId : 0;
    public string PlacementKey => _placement != null ? _placement.prefabName : null;
    public ChunkData PlacementChunk => _chunk;
    public float PlacedRespawnSeconds => placedRespawnSeconds;

    /// <summary>줍기 요청의 호스트 응답을 기다리는 중인지 (중복 요청 방지).</summary>
    public bool IsPickupPending => Time.unscaledTime < _pickupPendingUntil;

    public void MarkPickupPending() => _pickupPendingUntil = Time.unscaledTime + PickupPendingTimeout;

    public void ClearPickupPending() => _pickupPendingUntil = 0f;

    /// <summary>월드 생성 배치 시 호출 (WorldObjectSpawner).</summary>
    public void InitializeDispose(DisposeData dispose, ChunkData chunk)
    {
        if (_itemSO != null) Init(_itemSO);
        _placement = dispose;
        _chunk = chunk;
        NetworkDropId = 0;
        ClearPickupPending();

        // 풀에서 재사용된 오브젝트는 이전 수명의 수량(일부만 주운 스택 등)과 장착 뷰 상태를 들고 있다
        if (itemData is IStackable stackable)
            stackable.stackCount = _prefabStackCount;
        if (ItemDataSO != null)
            ResetWorldViewState(ItemDataSO.itemType);
    }

    /// <summary>풀에서 재사용돼 드롭 등 다른 용도로 쓰일 때 이전 배치 정보를 지운다.</summary>
    public void ClearWorldState()
    {
        _placement = null;
        _chunk = null;
        NetworkDropId = 0;
        ClearPickupPending();
    }

    /// <summary>SO 참조. itemData가 있으면 거기서, 없으면 _itemSO 직접 반환.</summary>
    public ItemDataSO ItemDataSO => itemData?.data ?? _itemSO;

    // 풀링된 오브젝트가 장착 뷰(손 위치 맞춤용 축소/회전)로 쓰인 뒤에도
    // 로컬 스케일/회전이 남아있으므로, 최초 생성 시점의 값을 기억해뒀다가 월드 배치 시 복원한다.
    [System.NonSerialized] private Vector3 _originalLocalScale = Vector3.one;
    [System.NonSerialized] private Quaternion _originalLocalRotation = Quaternion.identity;
    [System.NonSerialized] private bool _originalTransformCached;

    private void Awake()
    {
        CacheOriginalLocalTransform();
        _prefabStackCount = Mathf.Max(1, stackCount);
        Init();
    }

    private void CacheOriginalLocalTransform()
    {
        if (_originalTransformCached) return;
        _originalLocalScale = transform.localScale;
        _originalLocalRotation = transform.localRotation;
        _originalTransformCached = true;
    }

    /// <summary>장착 뷰 등으로 변형된 로컬 스케일/회전을 프리팹 원본 상태로 되돌린다.
    /// 위치는 호출부에서 별도로 설정한다 (드롭/월드 배치 시 사용).</summary>
    public void ResetToWorldTransform()
    {
        CacheOriginalLocalTransform();
        transform.localScale = _originalLocalScale;
        transform.localRotation = _originalLocalRotation;
    }

    protected virtual void Init()
    {
        if (_itemSO == null)
        {
            Debug.LogWarning($"[Item] {gameObject.name}: ItemDataSO가 연결되지 않았습니다!");
            return;
        }

        stackCount = Mathf.Max(1, stackCount);
        itemData = ItemData.CreateFromSO(_itemSO, stackCount);
        spoilDeadline = _itemSO.expirationTime > 0f ? SavePlayClock.Now + _itemSO.expirationTime * 60f : -1f;

        ResetWorldViewState(_itemSO.itemType);

        BindTorchLight();
    }

    /// <summary>
    /// 풀에서 재사용되는 오브젝트가 장착 뷰(ViewModel 레이어, 비활성 콜라이더) 상태로
    /// 남아있을 수 있으므로, 월드에 놓일 때마다 레이어/콜라이더를 재귀적으로 원복한다.
    /// </summary>
    private void ResetWorldViewState(ItemType itemType)
    {
        int layer = ItemTypeToLayer(itemType);
        SetLayerRecursively(transform, layer);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
            colliders[i].isTrigger = true;
        }
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    // 12=Structure, 17=Resource, 18=Booty, 19=Food(Dish/Special 포함), 20=Equipment(SurvivalTool/CombatGear)
    private static int ItemTypeToLayer(ItemType type) => type switch
    {
        ItemType.Resource     => 17,
        ItemType.Booty        => 18,
        ItemType.Food         => 19,
        ItemType.Dish         => 19,
        ItemType.Special      => 19,
        ItemType.SurvivalTool => 20,
        ItemType.CombatGear   => 20,
        ItemType.Structure    => 12,
        _                     => 17
    };

    private void BindTorchLight()
    {
        if (itemData is not Item_SurvivalTool tool) return;
        if (tool.survivalToolType != SurvivalToolType.Torch) return;

        if (_torchLight == null)
            _torchLight = GetComponentInChildren<Light>();

        if (_torchLight != null)
        {
            _torchLight.enabled = tool.IsLit;
            tool.OnTorchToggled += SetTorchLight;
        }
        else
        {
            Debug.LogWarning($"[Item] {gameObject.name}: 횃불 Light 컴포넌트를 찾을 수 없습니다.");
        }
    }

    private void SetTorchLight(bool on)
    {
        if (_torchLight != null)
            _torchLight.enabled = on;
    }

    private void OnDestroy()
    {
        if (itemData is Item_SurvivalTool tool)
            tool.OnTorchToggled -= SetTorchLight;
    }

    /// <summary>코드로 동적 생성할 때 SO를 주입한다.</summary>
    public virtual void Init(ItemDataSO so)
    {
        if (itemData is Item_SurvivalTool previousTool) previousTool.OnTorchToggled -= SetTorchLight;
        _itemSO = so;
        stackCount = 1;
        itemData = ItemData.CreateFromSO(so, stackCount);
        spoilDeadline = so.expirationTime > 0f ? SavePlayClock.Now + so.expirationTime * 60f : -1f;

        ResetWorldViewState(so.itemType);

        BindTorchLight();
    }
}

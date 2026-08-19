using UnityEngine;

/// <summary>
/// 월드에 존재하는 아이템 오브젝트의 베이스 컴포넌트.
/// Inspector에서 ItemDataSO를 연결하면 Awake 시점에 런타임 데이터(ItemData)를 자동 생성한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Item : MonoBehaviour
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

    [System.NonSerialized]
    public ItemData itemData;  // 런타임 전용 — Awake에서 _itemSO 기반으로 생성

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
        _itemSO = so;
        stackCount = 1;
        itemData = ItemData.CreateFromSO(so, stackCount);

        ResetWorldViewState(so.itemType);

        BindTorchLight();
    }
}

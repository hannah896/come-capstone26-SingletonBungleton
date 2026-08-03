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

    private void Awake()
    {
        Init();
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

        gameObject.layer = ItemTypeToLayer(_itemSO.itemType);

        // 줍기용 콜라이더를 트리거로 설정
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        BindTorchLight();
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
        gameObject.layer = ItemTypeToLayer(so.itemType);

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        BindTorchLight();
    }
}

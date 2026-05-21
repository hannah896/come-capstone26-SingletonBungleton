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

    [System.NonSerialized]
    public ItemData itemData;  // 런타임 전용 — Awake에서 _itemSO 기반으로 생성

    /// <summary>SO 참조. itemData가 있으면 거기서, 없으면 _itemSO 직접 반환.</summary>
    public ItemDataSO ItemDataSO => itemData?.data ?? _itemSO;

    private float _createdTime;

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

        itemData = ItemData.CreateFromSO(_itemSO);
        _createdTime = Time.time;

        // 줍기용 콜라이더를 트리거로 설정
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>코드로 동적 생성할 때 SO를 주입한다.</summary>
    public virtual void Init(ItemDataSO so)
    {
        _itemSO = so;
        itemData = ItemData.CreateFromSO(so);
        _createdTime = Time.time;
    }
}

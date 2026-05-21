using UnityEngine;

/// 게임 내 실제 존재하는 아이템 오브젝트의 베이스 클래스
/// 월드에 존재하는 아이템 프리팹에 붙는 컴포넌트임

[RequireComponent(typeof(Collider))]  // 줍기용 콜라이더 필수
public class Item : MonoBehaviour
{
    [Header("=== 아이템 데이터 ===")]
    [Tooltip("아이템 원본 데이터 (SO 연결)")]
    public ItemDataSO itemData;

    [Min(0)]
    public int stackCount = 1;

    public int StackCount { get => stackCount; protected set => stackCount = Mathf.Max(0, value); }
    public ItemDataSO ItemDataSO { get => itemData; protected set => itemData = value; }

    private float _createdTime;  // 생성 시점 (신선도 계산용 - 추후 사용)

    #region 초기화
    private void Awake()
    {
        Init();
    }

    /// 프리팹에 SO 연결 (Inspector에서 설정)
    protected virtual void Init()
    {
        if (itemData == null)
        {
            Debug.LogWarning($"[Item] {gameObject.name}: ItemDataSO가 연결되지 않았습니다!");
            return;
        }
        StackCount = 1;
        _createdTime = Time.time;
    }

    /// 코드로 동적 생성할 때 데이터를 주입하는 경우에
    public virtual void Init(ItemDataSO data)
    {
        itemData = data;
        StackCount = 1;
        _createdTime = Time.time;
    }
    #endregion
}

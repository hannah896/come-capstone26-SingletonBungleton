using UnityEngine;

/// 게임 내 실제 존재하는 아이템 인스턴스의 베이스 클래스
/// 월드에 존재하는 아이템 프리팹에 붙는 컴포넌트임

[RequireComponent(typeof(Collider))]  // 줍기용 콜라이더 필수
public abstract class Item : MonoBehaviour
{
    [Header("=== 아이템 데이터 ===")]
    [Tooltip("아이템 원본 데이터 (SO 연결)")]
    public ItemDataSO itemData;

    [Tooltip("현재 보유 수량")]
    public int stackCount = 1;

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
        stackCount = 1;
        _createdTime = Time.time;
    }

    /// 코드로 동적 생성할 때 데이터를 주입하는 경우에
    public virtual void Init(ItemDataSO data)
    {
        itemData = data;
        stackCount = 1;
        _createdTime = Time.time;
    }
    #endregion



    #region 중첩 관리

    /// 다른 아이템과 겹칠 수 있는지 확인
    public virtual bool CanStackWith(Item other)
    {
        if (other == null) return false;
        if (other.itemData != this.itemData) return false;       // 같은 종류인지
        if (!itemData.isStackable) return false;                  // 겹치기 허용인지
        if (stackCount >= itemData.maxStack) return false;        // 자리 있는지
        return true;
    }

    /// 수량 추가. 반환값 = 넘쳐서 못 담은 수량
    public int AddStack(int amount)
    {
        int space = itemData.maxStack - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;  // 초과분 반환
    }

    /// 수량 제거. 반환값 = 실제로 제거된 수량
    public int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }
    #endregion



    #region 생존 효과 (음식)

    /// 먹었을 때 PlayerStatus에 배고픔/체력/Ego 회복
    protected virtual void ApplySurvivalEffects(PlayerStatus stat)
    {
        if (stat == null) return;
        if (itemData.hungerRestore > 0) stat.RestoreHunger(itemData.hungerRestore);
        if (itemData.healthRestore > 0) stat.RestoreHp(itemData.healthRestore);
        if (itemData.egoRestore > 0) stat.RestoreEgo(itemData.egoRestore);
    }
    #endregion

    public override string ToString()
    {
        return $"{itemData.itemName} x{stackCount}";
    }
}

using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 몬스터·동물 공통 베이스. 스탯(MobStatus)·드랍(DropTableSO)·피격(IDamageable)·사망 처리를 담당한다.
/// 상태(FSM) 처리는 여기서 다루지 않고 Monster/Animal이 전담한다. Mob은 매 프레임/사망 시점만 훅으로 넘긴다.
/// </summary>
public abstract class Mob : MonoBehaviour, IDamageable, IPoolable
{
    #region Status & Data
    [SerializeField] protected EntityStatData statData;
    [SerializeField] protected DropTableSO dropTable;
    #endregion

    #region Animation
    [SerializeField] protected Animator animator;
    #endregion

    protected MobStatus status;
    private bool loopHooked;

    #region Properties
    public MobStatus Status => status;
    public Animator Animator => animator;
    #endregion

    protected virtual void Awake()
    {
        InitStatus();
    }

    /// <summary>
    /// statData로부터 런타임 스탯(MobStatus)을 새로 만든다.
    /// 씬 배치 Mob은 Awake에서, 풀링 Mob은 OnSpawn에서 호출돼 매 스폰마다 풀 HP로 리셋된다.
    /// statData가 아직 없으면(스폰 시 SO 주입 대기 중) 아무것도 하지 않는다.
    /// </summary>
    protected void InitStatus()
    {
        if (statData == null) return; // ApplyStatData로 주입될 때까지 대기

        status = new MobStatus(statData);
        status.OnDead += HandleDead;
    }

    /// <summary>
    /// 스폰 시점에 외부 SO로 스탯을 주입한다. statData를 교체하고 런타임 스탯을 새로 만든다.
    /// 프리팹에 statData를 박아두지 않고, 종류별 SO를 골라 채우는 흐름에 사용한다.
    /// </summary>
    public virtual void ApplyStatData(EntityStatData data)
    {
        statData = data;
        InitStatus();
    }

    #region IPoolable
    // 풀에서 꺼낼 때마다 스탯을 풀 HP로 되돌린다(죽은 채 재사용 방지). 자식은 base 호출 후 상태 리셋.
    public virtual void OnSpawn()   => InitStatus();
    public virtual void OnDespawn() { }
    #endregion

    protected virtual void OnEnable()
    {
        if (loopHooked) return;
        Main.Loop.OnGameUpdate += HandleGameUpdate;
        loopHooked = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    protected virtual void OnDestroy()
    {
        if (!loopHooked) return;
        if (Main.Loop != null)
            Main.Loop.OnGameUpdate -= HandleGameUpdate;
        loopHooked = false;
    }

    private void HandleGameUpdate(float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        OnGameUpdate(deltaTime);
    }

    /// <summary>매 게임 프레임 자식 처리(상태 구동 등). 기본 구현은 비어 있다.</summary>
    protected virtual void OnGameUpdate(float deltaTime) { }

    #region IDamageable
    public virtual bool CanDamage(DamageContext context)
        => status != null && !status.IsDead && context.Amount > 0;

    public virtual void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;
        status.TakeDamage(context.Amount);
    }
    #endregion

    /// <summary>
    /// 사망 시 처리: 자식 사망 훅 → 드랍 스폰 → 디스폰.
    /// </summary>
    protected virtual void HandleDead()
    {
        OnDeath();
        dropTable?.Spawn(transform.position).Forget();
        Extensions.Despawn(gameObject);
    }

    /// <summary>사망 순간 자식 처리(예: 사망 상태 전환). 기본 구현은 비어 있다.</summary>
    protected virtual void OnDeath() { }
}

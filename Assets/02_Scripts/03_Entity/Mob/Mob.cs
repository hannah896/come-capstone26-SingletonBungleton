using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 몬스터·동물 공통 베이스. 스탯(MobStatus)·드랍(DropTableSO)·피격(IDamageable)·사망 처리를 담당한다.
/// 상태(FSM) 처리는 여기서 다루지 않고 Monster/Animal이 전담한다. Mob은 매 프레임/사망 시점만 훅으로 넘긴다.
/// </summary>
public abstract class Mob : MonoBehaviour, IDamageable
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
        if (statData != null)
        {
            status = new MobStatus(statData);
            status.OnDead += HandleDead;
        }
        else
        {
            Debug.LogError($"[{name}] statData가 비어 있습니다.", this);
        }
    }

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

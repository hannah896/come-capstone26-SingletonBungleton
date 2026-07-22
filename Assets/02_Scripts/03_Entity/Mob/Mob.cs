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

    #region Death
    [Header("사망")]
    [Tooltip("사망 애니메이션 끝에 심은 Animation Event가 OnDeathAnimEnd()를 부르면 드랍/디스폰이 일어난다. " +
             "이 값은 이벤트가 오지 않을 때(미연결·전환 누락) 시체가 영원히 남지 않도록 하는 최대 대기 시간(초). " +
             "0이면 연출 없이 즉시 사라진다.")]
    [SerializeField] protected float deathFallbackTimeout = 5f;
    #endregion

    protected MobStatus status;
    private bool loopHooked;
    private bool isDying;
    private float deathTimer;

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
        OnStatusInitialized();
    }

    /// <summary>status가 (재)생성될 때마다 호출된다. 자식은 여기서 OnDamaged 등 추가 이벤트를 구독한다.</summary>
    protected virtual void OnStatusInitialized() { }

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
    public virtual void OnSpawn()
    {
        isDying = false;
        deathTimer = 0f;
        InitStatus();
    }

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

        // 사망 연출 중에는 일반 로직(추적/공격 등)을 돌리지 않는다.
        // 정상 흐름의 마무리는 OnDeathAnimEnd(Animation Event)가 맡고, 여기서는 그게 끝내 안 왔을 때만 걷어낸다.
        if (isDying)
        {
            deathTimer -= deltaTime;
            if (deathTimer <= 0f) FinishDeath();
            return;
        }

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
    /// 사망 시 처리: 자식 사망 훅(사망 상태 전환 → 연출 재생)까지만 하고 정리는 미룬다.
    /// 여기서 바로 디스폰하면 같은 프레임에 오브젝트가 풀로 돌아가 사망 애니메이션이 보이지 않는다.
    /// 실제 드랍/디스폰은 사망 애니메이션의 Animation Event(OnDeathAnimEnd)가 트리거한다.
    /// </summary>
    protected virtual void HandleDead()
    {
        if (isDying) return; // 중복 사망 알림 방어

        isDying = true;
        deathTimer = deathFallbackTimeout;
        OnDeath();

        if (deathTimer <= 0f) FinishDeath(); // 연출을 쓰지 않는 Mob은 즉시 정리
    }

    /// <summary>
    /// 사망 애니메이션 끝에 심은 Animation Event가 호출한다.
    /// Animation Event는 Animator와 같은 GameObject의 컴포넌트만 부를 수 있으므로, 이 메서드는 public이어야 한다.
    /// 사망 중이 아닐 때(살아있는 몹의 클립에 이벤트가 잘못 남은 경우) 들어온 호출은 무시한다.
    /// </summary>
    public void OnDeathAnimEnd()
    {
        if (!isDying) return;
        FinishDeath();
    }

    /// <summary>사망 연출이 끝난 시점. 드랍을 스폰하고 풀로 반환한다.</summary>
    private void FinishDeath()
    {
        isDying = false;

        if (CanSpawnDrops)
            dropTable?.Spawn(transform.position).Forget();

        Extensions.Despawn(gameObject);
    }

    /// <summary>
    /// 드랍을 스폰해도 되는 피어인지. 멀티플레이에서 클라까지 드랍을 만들면 아이템이 중복된다.
    /// 기본은 true이고, 네트워크로 복제되는 Mob(Monster)이 호스트 전용으로 좁힌다.
    /// </summary>
    protected virtual bool CanSpawnDrops => true;

    /// <summary>사망 순간 자식 처리(예: 사망 상태 전환). 기본 구현은 비어 있다.</summary>
    protected virtual void OnDeath() { }

    /// <summary>사망 연출 재생. 애니메이터를 쓰는 자식(Monster)이 오버라이드한다.</summary>
    public virtual void PlayDeadAnim() { }
}

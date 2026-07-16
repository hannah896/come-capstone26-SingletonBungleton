using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 적대 Mob. 플레이어를 인지·추적·공격한다.
/// 상태 전환 결정은 MonsterStateMachine + 각 MonsterState가, 실제 메카닉(감지/이동/공격)은 이 본체가 담당한다.
/// </summary>
public class Monster : Mob
{
    /// <summary>
    /// 풀에서 몬스터를 스폰하고 SO 스탯을 주입해 반환한다.
    /// 프리팹은 모델/콜라이더/애니메이터만 갖추고, 스탯은 종류별 MonsterStatData로 이 시점에 채운다.
    /// </summary>
    public static async UniTask<Monster> SpawnAsync(string key, MonsterStatData stat, Vector3 position, Transform parent = null)
    {
        var monster = await Extensions.SpawnAsync<Monster>(key, parent);
        if (monster == null) return null;

        monster.transform.position = position;
        if (stat != null) monster.ApplyStatData(stat);
        return monster;
    }

    #region Inspector
    [Header("타게팅")]
    [Tooltip("플레이어를 탐지할 레이어. 비워두면(Everything) Player 컴포넌트로만 필터링")]
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("애니메이션 Bool 파라미터명 (컨트롤러가 Entry에서 이 Bool로 분기한다. 없으면 비워둠 - 비어 있으면 건드리지 않음)")]
    [SerializeField] private string idleBool = "";
    [SerializeField] private string moveBool = "";
    [SerializeField] private string attackBool = "";
    [SerializeField] private string deadBool = "";
    [SerializeField] private string hitBool = "";

    [Header("피격 리액션")]
    [Tooltip("Hit 상태가 유지되는 시간(초). 공격 상태 중 피격 시에는 무시된다.")]
    [SerializeField] private float hitDuration = 0.4f;

    [Header("스폰 연출")]
    [Tooltip("등장 연출을 쓰는 몬스터인지. 컨트롤러의 기본(Default) 상태가 스폰 애니메이션이라 재생은 자동으로 되고, " +
             "여기서는 연출이 끝날 때까지 행동을 미룰지만 정한다. 끄면 스폰 즉시 Idle로 시작한다.")]
    [SerializeField] private bool useSpawnAnim = false;
    [Tooltip("스폰 애니메이션 끝에 심은 Animation Event가 OnSpawnAnimEnd()를 부르면 Idle로 넘어간다. " +
             "이 값은 이벤트가 오지 않을 때(미연결 등) 몬스터가 Spawn 상태에 갇히지 않도록 하는 최대 대기 시간(초).")]
    [SerializeField] private float spawnFallbackTimeout = 3f;
    #endregion

    protected MonsterStateMachine stateMachine;
    private Player target;

    private MonsterStatData MonsterData => statData as MonsterStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>현재 추적 중인 타깃. 파생 몬스터/전용 상태에서 읽기 전용으로 사용한다.</summary>
    public Player Target => target;

    public float DetectRange => MonsterData != null ? MonsterData.DetectRange : 0f;
    public float AttackRange => MonsterData != null ? MonsterData.AttackRange : 0f;
    public float FOV => MonsterData != null ? MonsterData.FOV : 360f;
    public float TurnSpeed => MonsterData != null ? MonsterData.TurnSpeed : 540f;
    public float MinAttackPeriod => status != null ? status.MinAttackPeriod : 1f;

    public string IdleBool => idleBool;
    public string MoveBool => moveBool;
    public string AttackBool => attackBool;
    public string DeadBool => deadBool;
    public string HitBool => hitBool;
    public float HitDuration => hitDuration;
    /// <summary>등장 연출이 끝날 때까지 Spawn 상태에서 대기할지. false면 스폰 즉시 Idle로 시작한다.</summary>
    public bool UseSpawnAnim => useSpawnAnim;
    /// <summary>스폰 Animation Event가 오지 않을 때 Idle로 강제 전환하기까지의 대기 시간(초).</summary>
    public float SpawnFallbackTimeout => spawnFallbackTimeout;
    #endregion

    #region Animation Bool Hash (Awake 시 1회 계산해 캐싱)
    private int idleBoolHash;
    private int moveBoolHash;
    private int attackBoolHash;
    private int deadBoolHash;
    private int hitBoolHash;

    public int IdleBoolHash => idleBoolHash;
    public int MoveBoolHash => moveBoolHash;
    public int AttackBoolHash => attackBoolHash;
    public int DeadBoolHash => deadBoolHash;
    public int HitBoolHash => hitBoolHash;

    /// <summary>빈 문자열은 "건드리지 않음"을 뜻하는 0 해시로 고정한다.</summary>
    protected static int ToAnimHash(string paramName)
        => string.IsNullOrEmpty(paramName) ? 0 : Animator.StringToHash(paramName);
    #endregion

    protected override void Awake()
    {
        base.Awake();
        idleBoolHash = ToAnimHash(idleBool);
        moveBoolHash = ToAnimHash(moveBool);
        attackBoolHash = ToAnimHash(attackBool);
        deadBoolHash = ToAnimHash(deadBool);
        hitBoolHash = ToAnimHash(hitBool);
        CacheAnimBoolParams();
        stateMachine = CreateStateMachine();
    }

    /// <summary>status가 (재)생성될 때마다 OnDamaged를 구독해 피격 리액션을 건다.</summary>
    protected override void OnStatusInitialized()
    {
        base.OnStatusInitialized();
        if (status != null)
            status.OnDamaged += HandleDamaged;
    }

    /// <summary>
    /// 피격 시 호출. 공격 중이면(IsAttackState) 데미지만 받고 Hit 상태로는 넘어가지 않는다.
    /// 공격 중이 아닐 때만 Hit 상태로 전환한다.
    /// </summary>
    private void HandleDamaged(float amount)
    {
        if (stateMachine == null || stateMachine.CurrentState == null) return;
        if (stateMachine.CurrentState.IsAttackState) return;
        stateMachine.ToHit();
    }

    // 풀에서 재사용될 때: 스탯(base) + 타깃/상태머신을 초기 상태로 되돌린다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        target = null;
        stateMachine = CreateStateMachine();
    }

    /// <summary>
    /// 상태머신 생성 팩토리. 종류별 몬스터는 오버라이드해 전용 머신(상태 그래프)으로 교체한다.
    /// </summary>
    protected virtual MonsterStateMachine CreateStateMachine()
        => new MonsterStateMachine(this);

    // 스탯 SO 주입 시 드롭테이블(MonsterStatData.DropTable)도 함께 세팅한다.
    public override void ApplyStatData(EntityStatData data)
    {
        base.ApplyStatData(data);
        if (data is MonsterStatData m && m.DropTable != null)
            dropTable = m.DropTable;
    }

    protected override void OnGameUpdate(float deltaTime)
        => stateMachine.OnUpdate(deltaTime);

    protected override void OnDeath()
        => stateMachine.ToDead();

    #region Targeting
    /// <summary>DetectRange 내에서 FOV를 만족하는 가장 가까운 플레이어를 탐색해 타깃으로 잡는다. 성공 시 true.</summary>
    public bool AcquireTarget()
    {
        target = null;
        if (DetectRange <= 0f) return false;

        var hits = Physics.OverlapSphere(transform.position, DetectRange, targetMask, QueryTriggerInteraction.Collide);
        float bestSqr = float.MaxValue;

        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || player.Stat == null || player.Stat.IsDead) continue;
            if (!InFieldOfView(player.transform.position)) continue;

            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                target = player;
            }
        }

        return target != null;
    }

    /// <summary>현재 타깃이 추적 가능한 상태인지(존재·생존·DetectRange 이내).</summary>
    public bool IsTargetValid()
    {
        if (target == null || !target.isActiveAndEnabled) return false;
        if (target.Stat == null || target.Stat.IsDead) return false;
        return PlanarDistanceToTarget() <= DetectRange;
    }

    /// <summary>타깃이 공격 사거리 안에 있는지.</summary>
    public bool IsTargetInAttackRange()
        => target != null && PlanarDistanceToTarget() <= AttackRange;

    public void ClearTarget() => target = null;

    /// <summary>타깃과의 수평(Y 무시) 거리. 파생 몬스터의 사거리 판정에 사용한다.</summary>
    protected float PlanarDistanceToTarget()
    {
        Vector3 d = target.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    private bool InFieldOfView(Vector3 worldPos)
    {
        if (FOV >= 360f) return true;
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return true;
        float angle = Vector3.Angle(transform.forward, dir);
        return angle <= FOV * 0.5f;
    }
    #endregion

    #region Locomotion / Combat
    /// <summary>타깃 쪽으로 회전하며 MoveSpeed로 전진한다(추적).</summary>
    public void ChaseStep(float deltaTime)
    {
        if (target == null || status == null) return;

        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector3 nd = dir.normalized;
        RotateTowards(nd, deltaTime);
        transform.position += nd * status.MoveSpeed * deltaTime;
    }

    /// <summary>이동 없이 타깃을 바라보도록 회전만 한다(공격 중 정렬).</summary>
    public void FaceTargetStep(float deltaTime)
    {
        if (target == null) return;
        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        RotateTowards(dir.normalized, deltaTime);
    }

    private void RotateTowards(Vector3 flatDir, float deltaTime)
    {
        Quaternion look = Quaternion.LookRotation(flatDir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, TurnSpeed * deltaTime);
    }

    /// <summary>현재 타깃에게 1회 공격 데미지를 적용한다.</summary>
    public void PerformAttack()
    {
        if (target == null || status == null) return;
        if (!target.TryGetComponent<IDamageable>(out var dmg)) return;

        int amount = Mathf.Max(Mathf.RoundToInt(status.Attack), 0);
        var ctx = new DamageContext(
            instigator: gameObject,
            point: target.transform.position,
            amount: amount,
            toolId: string.Empty);

        if (dmg.CanDamage(ctx))
            dmg.ApplyDamage(ctx);
    }
    #endregion

    #region Animation (선택)
    // 컨트롤러에 실제로 존재하는 Bool 파라미터 해시. 없는 이름에 SetBool을 걸면 경고가 나므로 미리 걸러낸다.
    private readonly HashSet<int> animBoolHashes = new HashSet<int>();

    private void CacheAnimBoolParams()
    {
        animBoolHashes.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool)
                animBoolHashes.Add(param.nameHash);
        }
    }

    /// <summary>
    /// 지금 재생할 애니메이션 Bool 하나만 켜고 나머지는 끈다.
    /// 컨트롤러가 Entry에서 Bool로 분기하고 각 상태는 자기 Bool이 꺼져야 빠져나오는 구조라,
    /// "한 번에 하나만 true"를 지켜야 상태가 엉키지 않는다.
    /// 해시가 0(빈 이름)이거나 컨트롤러에 없는 파라미터면 아무것도 하지 않는다.
    /// </summary>
    public void PlayAnim(int boolHash)
    {
        if (animator == null || boolHash == 0) return;
        if (!animBoolHashes.Contains(boolHash)) return;

        foreach (int hash in animBoolHashes)
            animator.SetBool(hash, hash == boolHash);
    }

    /// <summary>애니메이션 Bool을 모두 끈다. 스폰 시 기본(Default) 상태로 시작시키기 위해 사용한다.</summary>
    public void ClearAnimBools()
    {
        if (animator == null) return;

        foreach (int hash in animBoolHashes)
            animator.SetBool(hash, false);
    }

    /// <summary>사망 연출: Dead Bool을 켠다.</summary>
    public override void PlayDeadAnim()
        => PlayAnim(deadBoolHash);

    /// <summary>
    /// 스폰(등장) 애니메이션 끝에 심은 Animation Event가 호출한다.
    /// Animation Event는 Animator와 같은 GameObject의 컴포넌트만 부를 수 있으므로, 이 메서드는 public이어야 한다.
    /// 스폰 연출 중이 아닐 때(다른 클립에 이벤트가 잘못 남은 경우) 들어온 호출은 무시한다.
    /// </summary>
    public void OnSpawnAnimEnd()
    {
        if (stateMachine == null || !(stateMachine.CurrentState is MonsterSpawnState)) return;
        stateMachine.ToIdle();
    }
    #endregion
}

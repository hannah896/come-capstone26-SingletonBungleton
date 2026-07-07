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

    [Header("애니메이션 상태명 (없으면 비워둠 - 비어 있으면 재생 안 함)")]
    [SerializeField] private string idleAnim = "";
    [SerializeField] private string moveAnim = "";
    [SerializeField] private string attackAnim = "";
    [SerializeField] private string deadAnim = "";
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

    public string IdleAnim => idleAnim;
    public string MoveAnim => moveAnim;
    public string AttackAnim => attackAnim;
    public string DeadAnim => deadAnim;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        stateMachine = CreateStateMachine();
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

    private float PlanarDistanceToTarget()
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
    /// <summary>상태명이 비어 있지 않고 컨트롤러에 해당 스테이트가 있을 때만 크로스페이드 재생(경고 없음).</summary>
    public void PlayAnim(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(hash, 0.1f);
    }
    #endregion
}

using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미스치프. 두 가지 공격을 가진 원거리+근접 하이브리드 몬스터.
/// - 프로젝타일: 사거리(AttackRange)가 넓은 대신 내부 쿨타임(ProjectileCooldown)이 길다.
/// - 슬래시: 내부 쿨이 없는 대신 사정거리(SlashRange)가 짧다. MinAttackPeriod 주기로만 제한된다.
/// 공격 선택은 MischiefAttackState가, 발사/쿨타임 메카닉은 이 본체가 담당한다.
/// </summary>
public class Mischief : Monster
{
    [Header("애니메이션 Bool 파라미터명 (IMP_S 컨트롤러 기준, 없으면 비워둠)")]
    [SerializeField] private string rangeAttackBool = "";
    [Tooltip("도약 애니메이션 Bool 파라미터명. IMP_S에 도약용 파라미터가 없으면 비워둔다(연출은 생략, 이동은 정상)")]
    [SerializeField] private string jumpBool = "";

    [Header("도약(Leap) - 루트 모션 OFF, 스크립트로 실제 이동")]
    [Tooltip("도약 정점 높이(m). 루트가 이만큼 떠오르므로 콜라이더도 함께 올라간다")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("한 번 도약으로 타깃 쪽으로 이동하는 수평 거리(m)")]
    [SerializeField] private float jumpDistance = 3f;
    [Tooltip("도약 한 번에 걸리는 시간(초)")]
    [SerializeField] private float jumpDuration = 0.6f;
    [Tooltip("도약 사이의 재사용 대기시간(초)")]
    [SerializeField] private float jumpCooldown = 3f;

    private int rangeAttackBoolHash;
    private int jumpBoolHash;
    private float projectileCooldown;
    private float leapCooldown;

    private MischiefStatData MischiefData => statData as MischiefStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>근접 슬래시 사거리. AttackRange(프로젝타일 사거리)보다 짧다.</summary>
    public float SlashRange => MischiefData != null ? MischiefData.SlashRange : 1.5f;
    /// <summary>프로젝타일 내부 쿨타임이 끝나 발사 가능한지.</summary>
    public bool IsProjectileReady => projectileCooldown <= 0f;
    /// <summary>프로젝타일 발사 모션 동안 제자리에 고정되는 시간.</summary>
    public float ProjectileCastTime => MischiefData != null ? MischiefData.ProjectileCastTime : 1f;
    public float JumpHeight => jumpHeight;
    public float JumpDistance => jumpDistance;
    public float JumpDuration => jumpDuration;
    /// <summary>도약 재사용 대기가 끝나 다시 도약 가능한지.</summary>
    public bool IsLeapReady => leapCooldown <= 0f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        jumpBoolHash = ToAnimHash(jumpBool);
    }

    // 풀에서 재사용될 때 프로젝타일/도약 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        projectileCooldown = 0f;
        leapCooldown = 0f;
    }

    // 공격 상태를 MischiefAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new MischiefStateMachine(this);

    // 미스치프 전용 애니(원거리 공격/도약)를 매핑에 더한다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        MonsterAnimId.Jump        => jumpBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 프로젝타일/도약 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (projectileCooldown > 0f)
            projectileCooldown -= deltaTime;
        if (leapCooldown > 0f)
            leapCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 슬래시 사거리 안에 있는지.</summary>
    public bool IsTargetInSlashRange()
        => Target != null && PlanarDistanceToTarget() <= SlashRange;

    /// <summary>도약을 시작하며 재사용 대기시간을 건다.</summary>
    public void StartLeapCooldown() => leapCooldown = jumpCooldown;

    /// <summary>도약이 향할 수평 방향(단위 벡터). 타깃이 없으면 현재 전방.</summary>
    public Vector3 LeapDirection()
    {
        if (Target == null) return transform.forward;
        Vector3 d = Target.transform.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
    }

    /// <summary>
    /// 현재 타깃을 향해 프로젝타일을 발사하고 내부 쿨타임을 시작한다.
    /// 쿨타임 중이거나 타깃이 없으면 무시된다.
    /// </summary>
    public void FireProjectile()
    {
        if (Target == null || !IsProjectileReady || MischiefData == null) return;

        projectileCooldown = MischiefData.ProjectileCooldown;
        PlayAnim(MonsterAnimId.RangeAttack);
        FireProjectileAsync(Target).Forget();
    }

    private async UniTaskVoid FireProjectileAsync(Player target)
    {
        var data = MischiefData;
        var projectile = await Extensions.SpawnAsync<MonsterProjectile>(data.ProjectileKey);
        if (projectile == null) return;

        // 로드 대기 사이 타깃이 죽거나 사라졌으면 발사 취소
        if (target == null || !target.isActiveAndEnabled)
        {
            Extensions.Despawn(projectile.gameObject);
            return;
        }

        Vector3 origin = transform.position + Vector3.up * data.MuzzleHeight;
        Vector3 aim = target.transform.position + Vector3.up * data.MuzzleHeight;
        Vector3 dir = aim - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        float rawDamage = data.ProjectileDamage > 0f
            ? data.ProjectileDamage
            : (status != null ? status.Attack : data.AttackDamage);
        int damage = Mathf.Max(Mathf.RoundToInt(rawDamage), 0);

        projectile.Init(gameObject, origin, dir.normalized, data.ProjectileSpeed, damage, data.ProjectileLifeTime);
    }
}

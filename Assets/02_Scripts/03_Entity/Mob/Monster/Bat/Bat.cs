using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 외눈 박쥐(Cyclops Bat). 날아다니며 눈알 탄을 쏘고, 가까우면 문다.
/// - 눈알 탄: 사거리(AttackRange)가 넓은 대신 내부 쿨타임(ProjectileCooldown)이 있다. 발사 모션 중간에 탄이 나간다.
/// - 물기: 사거리(BiteRange)가 짧다. MinAttackPeriod 주기로만 제한된다. 모션 중간에 판정한다.
/// 공격 선택은 BatAttackState가, 발사/판정/쿨타임 메카닉은 이 본체가 담당한다.
///
/// CyClopBat 컨트롤러에는 이동 전용 Bool이 없다. 날갯짓 Idle이 곧 비행 모션이므로
/// 프리팹의 moveBool을 Idle로 두어 이동 중에도 Idle 클립을 재생한다.
/// </summary>
public class Bat : Monster
{
    [Header("외눈 박쥐 - 애니메이션 Bool 파라미터명 (CyClopBat 컨트롤러 기준, 없으면 비워둠)")]
    [SerializeField] private string rangeAttackBool = "ProjectileAttack";

    [Header("외눈 박쥐 - 애니메이터 상태 이름 (판정 타이밍을 클립 시작에 맞추는 데 사용)")]
    [SerializeField] private string biteStateName = "Bite Attack";
    [SerializeField] private string projectileStateName = "Projectile Attack";

    [Header("외눈 박쥐 - 비행 높이")]
    [Tooltip("모델을 띄우는 높이(m). 본체(루트)는 지면에 두고 루트 본만 애니메이션 뒤에 올린다. " +
             "본체를 띄우면 저장/불러오기 때마다 높이가 누적되므로 이 방식을 쓴다. 콜라이더 중심도 같은 만큼 올려 둘 것")]
    [SerializeField] private float hoverHeight = 0.8f;
    [Tooltip("띄울 루트 본 이름. 모든 본이 이 아래에 있어야 모델이 통째로 뜬다")]
    [SerializeField] private string hoverBoneName = "RigHead";
    [Tooltip("사망 시 땅으로 내려오는 속도(m/s)")]
    [SerializeField] private float fallSpeed = 2f;

    private int rangeAttackBoolHash;
    private int biteStateHash;
    private int projectileStateHash;
    private float projectileCooldown;

    private Transform hoverBone;
    private float currentHover;
    private bool lateHooked;
    private Vector3 lastHoverWritten;
    private float lastHoverOffset;
    private bool hasLastHover;

    private BatStatData BatData => statData as BatStatData;

    #region Properties (상태 클래스에서 사용)
    public float BiteRange => BatData != null ? BatData.BiteRange : 1.6f;
    public float BiteHitTime => BatData != null ? BatData.BiteHitTime : 0.3f;
    public float BiteDuration => BatData != null ? BatData.BiteDuration : 0.75f;
    public bool IsProjectileReady => projectileCooldown <= 0f;
    public float ProjectileReleaseTime => BatData != null ? BatData.ProjectileReleaseTime : 0.4f;
    public float ProjectileCastTime => BatData != null ? BatData.ProjectileCastTime : 0.85f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        biteStateHash = ToAnimHash(biteStateName);
        projectileStateHash = ToAnimHash(projectileStateName);

        hoverBone = string.IsNullOrEmpty(hoverBoneName) ? null : transform.Find(hoverBoneName);
        currentHover = hoverHeight;
        if (hoverBone != null && !lateHooked)
        {
            Main.Loop.OnLateUpdate += HandleLateUpdate;
            lateHooked = true;
        }
    }

    protected override void OnDestroy()
    {
        if (lateHooked && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnLateUpdate -= HandleLateUpdate;
        lateHooked = false;
        base.OnDestroy();
    }

    // 애니메이터가 루트 본 위치를 쓴 뒤에 높이를 더한다. 사망하면 땅으로 내려온다.
    private void HandleLateUpdate(float deltaTime)
    {
        if (hoverBone == null || !isActiveAndEnabled) return;

        float target = CurrentAnimId == MonsterAnimId.Dead ? 0f : hoverHeight;
        currentHover = Mathf.MoveTowards(currentHover, target, fallSpeed * deltaTime);

        // 애니메이터가 이번 프레임에 본 위치를 안 썼으면(화면 밖 컬링 등) 지난 프레임 오프셋이 남아 있으므로 먼저 뺀다.
        // 안 그러면 높이가 매 프레임 누적된다.
        Vector3 p = hoverBone.localPosition;
        if (hasLastHover && p == lastHoverWritten)
            p -= Vector3.up * lastHoverOffset;

        p += Vector3.up * currentHover;
        hoverBone.localPosition = p;
        lastHoverWritten = p;
        lastHoverOffset = currentHover;
        hasLastHover = true;
    }

    /// <summary>
    /// 애니메이터가 실제로 물기/발사 클립에 들어갔는지.
    /// Bool을 켠 뒤 Exit → Entry를 거쳐 클립이 시작되기까지 지연(0.25s~, 스폰 반복 중이면 더 길다)이 있어서,
    /// 판정 시간은 이 값이 true가 된 순간부터 센다. 애니메이터가 없으면 바로 true.
    /// </summary>
    public bool IsInAttackClip(bool projectile)
    {
        if (Animator == null) return true;
        int hash = projectile ? projectileStateHash : biteStateHash;
        if (hash == 0) return true;
        return Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
    }

    // 풀에서 재사용될 때 눈알 탄 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        projectileCooldown = 0f;
        currentHover = hoverHeight;
    }

    // 공격 상태를 BatAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new BatStateMachine(this);

    // 박쥐 전용 애니(눈알 탄)를 매핑에 더한다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 눈알 탄 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (projectileCooldown > 0f)
            projectileCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 물기 사거리 안에 있는지.</summary>
    public bool IsTargetInBiteRange()
        => Target != null && PlanarDistanceToTarget() <= BiteRange;

    /// <summary>눈알 탄 모션을 시작하며 내부 쿨타임을 건다. 실제 발사는 ReleaseTime 뒤 FireProjectile에서.</summary>
    public void StartProjectileCooldown()
        => projectileCooldown = BatData != null ? BatData.ProjectileCooldown : 3.5f;

    /// <summary>
    /// 물기 판정. 모션이 입을 다무는 순간 호출된다.
    /// 모션 도중 타깃이 물러났으면(사거리 1.2배 밖, 또는 정면 각도 밖) 빗나간다.
    /// </summary>
    public void BiteHit()
    {
        if (Target == null || !Target.IsAlive) return;
        if (PlanarDistanceToTarget() > BiteRange * 1.2f) return;

        Vector3 toTarget = Target.transform.position - transform.position;
        toTarget.y = 0f;
        float halfAngle = (BatData != null ? BatData.BiteAngle : 120f) * 0.5f;
        if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toTarget) > halfAngle) return;

        PerformAttack();
    }

    /// <summary>현재 타깃을 향해 눈알 탄을 발사한다. 타깃이 없으면 무시된다.</summary>
    public void FireProjectile()
    {
        if (Target == null || BatData == null) return;
        FireProjectileAsync(Target).Forget();
    }

    private async UniTaskVoid FireProjectileAsync(Player target)
    {
        var data = BatData;
        var projectile = await Extensions.SpawnAsync<MonsterProjectile>(data.ProjectileKey);
        if (projectile == null) return;

        // 로드 대기 사이 타깃이 죽거나 사라졌으면 발사 취소
        if (target == null || !target.isActiveAndEnabled)
        {
            Extensions.Despawn(projectile.gameObject);
            return;
        }

        Vector3 origin = transform.position + Vector3.up * data.MuzzleHeight + transform.forward * 0.3f;
        Vector3 aim = target.transform.position + Vector3.up * data.AimHeight;
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

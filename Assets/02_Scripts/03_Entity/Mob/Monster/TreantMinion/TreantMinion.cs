using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 트리앤트 미니언. (기획: docs/기획_트리앤트미니언_스킬로직.md)
///
/// "걷지 않고 구른다" — 숲 지역의 떼거리 잡몹.
/// 애니메이터가 이동(Run)에 Roll Forward 클립을 쓰고 근접 평타가 없는 구성이라,
/// 접근도 공격도 전부 구르기로 처리한다. 혼자서는 약하지만 무리로 굴러오면 사방에서 들이받는다.
///
/// 세 기술을 쿨타임으로 돌려 쓴다.
/// - 뿌리 속박(Cast Spell): 타깃 발밑에 장판을 깔아 발을 묶는 선행기
/// - 구르기 돌진(Roll Attack): 주력 딜기. 묶인 타깃을 들이받는다
/// - 도토리 투척(Projectile Attack): 둘 다 쿨일 때 쓰는 견제기
///
/// 기술 선택은 TreantMinionAttackState가, 발사/시전/쿨타임 메카닉은 이 본체가 담당한다.
/// </summary>
public class TreantMinion : Monster
{
    #region Inspector
    [Header("트리앤트 - 애니메이션 Bool 파라미터명 (Treant_Minion 컨트롤러 기준, 없으면 비워둠)")]
    [Tooltip("도토리 투척 (Projectile Attack). 컨트롤러의 PAttack")]
    [SerializeField] private string rangeAttackBool = "";
    [Tooltip("구르기 돌진 (Roll Attack). 컨트롤러의 RAttack")]
    [SerializeField] private string rollAttackBool = "";
    [Tooltip("뿌리 속박 시전 (Cast Spell). 컨트롤러의 CAttack")]
    [SerializeField] private string castSpellBool = "";
    #endregion

    private int rangeAttackBoolHash;
    private int rollAttackBoolHash;
    private int castSpellBoolHash;

    private float rollCooldown;
    private float projectileCooldown;
    private float snareCooldown;

    private TreantMinionStatData TreantData => statData as TreantMinionStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>돌진을 시작할 수 있는 최대 거리.</summary>
    public float RollRange => TreantData != null ? TreantData.RollRange : 8f;
    public float RollSpeed => TreantData != null ? TreantData.RollSpeed : 9f;
    public float RollDuration => TreantData != null ? TreantData.RollDuration : 0.55f;
    public float RollHitRadius => TreantData != null ? TreantData.RollHitRadius : 0.8f;
    public float RollRecoverTime => TreantData != null ? TreantData.RollRecoverTime : 0.4f;
    /// <summary>돌진 재사용 대기가 끝나 다시 구를 수 있는지.</summary>
    public bool IsRollReady => rollCooldown <= 0f;

    /// <summary>투척 모션 동안 제자리에 고정되는 시간.</summary>
    public float ProjectileCastTime => TreantData != null ? TreantData.ProjectileCastTime : 0.6f;
    /// <summary>투척 내부 쿨타임이 끝나 발사 가능한지.</summary>
    public bool IsProjectileReady => projectileCooldown <= 0f;

    /// <summary>뿌리 속박을 시전할 수 있는 최대 거리.</summary>
    public float SnareRange => TreantData != null ? TreantData.SnareRange : 8f;
    /// <summary>시전 모션 동안 제자리에 고정되는 시간.</summary>
    public float SnareCastTime => TreantData != null ? TreantData.SnareCastTime : 1f;
    /// <summary>뿌리 속박 내부 쿨타임이 끝나 시전 가능한지.</summary>
    public bool IsSnareReady => snareCooldown <= 0f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        rollAttackBoolHash = ToAnimHash(rollAttackBool);
        castSpellBoolHash = ToAnimHash(castSpellBool);
    }

    // 풀에서 재사용될 때 세 기술의 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        rollCooldown = 0f;
        projectileCooldown = 0f;
        snareCooldown = 0f;
    }

    // 공격 상태를 TreantMinionAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new TreantMinionStateMachine(this);

    // 트리앤트 전용 애니(투척/돌진/시전)를 매핑에 더한다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        MonsterAnimId.RollAttack  => rollAttackBoolHash,
        MonsterAnimId.CastSpell   => castSpellBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 세 기술의 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (rollCooldown > 0f)
            rollCooldown -= deltaTime;
        if (projectileCooldown > 0f)
            projectileCooldown -= deltaTime;
        if (snareCooldown > 0f)
            snareCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 돌진 사거리 안에 있는지.</summary>
    public bool IsTargetInRollRange()
        => Target != null && PlanarDistanceToTarget() <= RollRange;

    /// <summary>타깃이 뿌리 속박 사거리 안에 있는지.</summary>
    public bool IsTargetInSnareRange()
        => Target != null && PlanarDistanceToTarget() <= SnareRange;

    /// <summary>돌진을 시작하며 재사용 대기시간을 건다.</summary>
    public void StartRollCooldown()
    {
        if (TreantData != null)
            rollCooldown = TreantData.RollCooldown;
    }

    /// <summary>돌진이 향할 수평 방향(단위 벡터). 타깃이 없으면 현재 전방.</summary>
    public Vector3 RollDirection()
    {
        if (Target == null) return transform.forward;
        Vector3 d = Target.transform.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
    }

    /// <summary>
    /// 돌진 중 몸통에 닿은 플레이어에게 1회 데미지와 넉백을 준다.
    /// 한 번의 돌진에서 같은 플레이어를 여러 번 때리지 않도록, 명중 여부는 돌진 상태가 관리한다.
    /// </summary>
    /// <returns>명중한 플레이어. 아무도 닿지 않았으면 null.</returns>
    public Player RollHitStep()
    {
        var data = TreantData;
        if (data == null) return null;

        var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, RollHitRadius,
            ~0, QueryTriggerInteraction.Collide);

        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive) continue;

            float rawDamage = data.RollDamage > 0f
                ? data.RollDamage
                : (status != null ? status.Attack : data.AttackDamage);
            int damage = Mathf.Max(Mathf.RoundToInt(rawDamage), 0);

            var ctx = new DamageContext(
                instigator: gameObject,
                point: player.transform.position,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);

            // 들이받힌 쪽을 뒤로 밀어낸다. 구르는 통나무에 치인 느낌을 주는 핵심 연출이다.
            if (data.RollKnockback > 0f)
            {
                Vector3 push = player.transform.position - transform.position;
                push.y = 0f;
                if (push.sqrMagnitude < 0.0001f) push = transform.forward;
                player.Motor?.AddKnockback(push.normalized, data.RollKnockback, data.RollKnockbackDuration);
            }

            return player;
        }

        return null;
    }

    /// <summary>
    /// 현재 타깃을 향해 도토리를 던지고 내부 쿨타임을 시작한다.
    /// 쿨타임 중이거나 타깃이 없으면 무시된다.
    /// </summary>
    public void FireProjectile()
    {
        if (Target == null || !IsProjectileReady || TreantData == null) return;

        projectileCooldown = TreantData.ProjectileCooldown;
        PlayAnim(MonsterAnimId.RangeAttack);
        FireProjectileAsync(Target).Forget();
    }

    private async UniTaskVoid FireProjectileAsync(Player target)
    {
        var data = TreantData;
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

    /// <summary>
    /// 타깃 발밑에 뿌리 속박 장판을 깔고 내부 쿨타임을 시작한다.
    /// 쿨타임 중이거나 타깃이 없으면 무시된다.
    /// </summary>
    public void CastRootSnare()
    {
        if (Target == null || !IsSnareReady || TreantData == null) return;

        snareCooldown = TreantData.SnareCooldown;
        PlayAnim(MonsterAnimId.CastSpell);

        // 시전 시점의 타깃 발밑을 중심으로 고정한다(예고 시간 동안 플레이어가 벗어나면 피할 수 있다).
        CastRootSnareAsync(Target.transform.position).Forget();
    }

    private async UniTaskVoid CastRootSnareAsync(Vector3 center)
    {
        var data = TreantData;
        var snare = await Extensions.SpawnAsync<Meteor>(data.SnareKey);
        if (snare == null) return;

        int damage = Mathf.Max(Mathf.RoundToInt(data.SnareDamage), 0);

        snare.Init(gameObject, center, damage, data.SnareRadius, data.SnareWarningTime, data.SnareLingerTime,
            dotDamage: 0f, dotDuration: 0f, dotInterval: 1f,
            slowMultiplier: data.SnareSlowMultiplier, slowDuration: data.SnareSlowDuration);
    }
}

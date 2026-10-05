using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 트리앤트 트리. 숲 지역의 중형 근접 몬스터.
///
/// 느리게 걸어와 묵직한 나무 주먹으로 플레이어를 멀리 날려 보낸다.
/// 주먹이 닿지 않는 거리에서는 열매를 던져 견제한다.
/// - 나무 주먹(Punch Attack): 주력기. 전방 부채꼴 판정 + 강한 넉백
/// - 열매 투척(Projectile Attack): 주먹 사거리 밖에서 쓰는 견제기
///
/// 기술 선택은 TreantTreeAttackState가, 타격/발사/쿨타임 메카닉은 이 본체가 담당한다.
/// 주먹은 베이스 Attack 애니(attackBool = PunchAttack)를 쓰고, 투척만 전용 Bool을 더한다.
/// </summary>
public class TreantTree : Monster
{
    #region Inspector
    [Header("트리앤트 트리 - 애니메이션 Bool 파라미터명 (Treant_Tree 컨트롤러 기준, 없으면 비워둠)")]
    [Tooltip("열매 투척 (Projectile Attack). 컨트롤러의 ProjectileAttack")]
    [SerializeField] private string rangeAttackBool = "";
    #endregion

    private int rangeAttackBoolHash;

    private float punchCooldown;
    private float projectileCooldown;

    private TreantTreeStatData TreantData => statData as TreantTreeStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>주먹을 휘두르기 시작하는 거리.</summary>
    public float PunchRange => TreantData != null ? TreantData.PunchRange : 2.2f;
    /// <summary>모션 시작부터 타격까지의 시간.</summary>
    public float PunchHitTime => TreantData != null ? TreantData.PunchHitTime : 0.4f;
    /// <summary>주먹 모션 동안 제자리에 고정되는 시간.</summary>
    public float PunchDuration => TreantData != null ? TreantData.PunchDuration : 0.9f;
    /// <summary>주먹 쿨타임이 끝나 다시 휘두를 수 있는지.</summary>
    public bool IsPunchReady => punchCooldown <= 0f;

    /// <summary>투척 모션 동안 제자리에 고정되는 시간.</summary>
    public float ProjectileCastTime => TreantData != null ? TreantData.ProjectileCastTime : 0.8f;
    /// <summary>투척 내부 쿨타임이 끝나 발사 가능한지.</summary>
    public bool IsProjectileReady => projectileCooldown <= 0f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
    }

    // 풀에서 재사용될 때 두 기술의 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        punchCooldown = 0f;
        projectileCooldown = 0f;
    }

    // 공격 상태를 TreantTreeAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new TreantTreeStateMachine(this);

    // 투척 애니를 매핑에 더한다. (주먹은 베이스 Attack 매핑을 그대로 쓴다)
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 두 기술의 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (punchCooldown > 0f)
            punchCooldown -= deltaTime;
        if (projectileCooldown > 0f)
            projectileCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 주먹 사거리 안에 있는지.</summary>
    public bool IsTargetInPunchRange()
        => Target != null && PlanarDistanceToTarget() <= PunchRange;

    /// <summary>주먹 모션을 시작하고 쿨타임을 건다. 실제 타격은 PunchHitTime 뒤 PunchHit()에서 한다.</summary>
    public void StartPunch()
    {
        if (TreantData != null)
            punchCooldown = TreantData.PunchCooldown;
        PlayAnim(MonsterAnimId.Attack);
    }

    /// <summary>
    /// 주먹 타격 판정. 전방 부채꼴(PunchRange, PunchAngle) 안의 모든 플레이어에게 데미지와 넉백을 준다.
    /// 휘두르는 사이 타깃이 뒤로 빠졌으면 헛친다.
    /// </summary>
    public void PunchHit()
    {
        var data = TreantData;
        if (data == null) return;

        float rawDamage = data.PunchDamage > 0f
            ? data.PunchDamage
            : (status != null ? status.Attack : data.AttackDamage);
        int damage = Mathf.Max(Mathf.RoundToInt(rawDamage), 0);
        float halfAngle = data.PunchAngle * 0.5f;

        var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, data.PunchRange,
            ~0, QueryTriggerInteraction.Collide);

        // 한 플레이어가 콜라이더를 여러 개 가질 수 있으므로 중복 타격을 막는다.
        var struck = new HashSet<Player>();

        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive) continue;
            if (!struck.Add(player)) continue;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toPlayer) > halfAngle)
                continue;

            var ctx = new DamageContext(
                instigator: gameObject,
                point: player.transform.position,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);

            // 나무 주먹의 핵심 연출. 맞은 쪽을 멀리 날려 보낸다.
            if (data.PunchKnockback > 0f)
            {
                Vector3 push = toPlayer.sqrMagnitude < 0.0001f ? transform.forward : toPlayer.normalized;
                player.Motor?.AddKnockback(push, data.PunchKnockback, data.PunchKnockbackDuration);
            }
        }
    }

    /// <summary>
    /// 현재 타깃을 향해 열매를 던지고 내부 쿨타임을 시작한다.
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
}

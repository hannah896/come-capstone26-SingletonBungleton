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
    private float projectileCooldown;

    private MischiefStatData MischiefData => statData as MischiefStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>근접 슬래시 사거리. AttackRange(프로젝타일 사거리)보다 짧다.</summary>
    public float SlashRange => MischiefData != null ? MischiefData.SlashRange : 1.5f;
    /// <summary>프로젝타일 내부 쿨타임이 끝나 발사 가능한지.</summary>
    public bool IsProjectileReady => projectileCooldown <= 0f;
    #endregion

    // 풀에서 재사용될 때 프로젝타일 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        projectileCooldown = 0f;
    }

    // 공격 상태를 MischiefAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new MischiefStateMachine(this);

    // 프로젝타일 내부 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (projectileCooldown > 0f)
            projectileCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 슬래시 사거리 안에 있는지.</summary>
    public bool IsTargetInSlashRange()
        => Target != null && PlanarDistanceToTarget() <= SlashRange;

    /// <summary>
    /// 현재 타깃을 향해 프로젝타일을 발사하고 내부 쿨타임을 시작한다.
    /// 쿨타임 중이거나 타깃이 없으면 무시된다.
    /// </summary>
    public void FireProjectile()
    {
        if (Target == null || !IsProjectileReady || MischiefData == null) return;

        projectileCooldown = MischiefData.ProjectileCooldown;
        PlayAnim(AttackAnim);
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

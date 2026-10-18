/// <summary>
/// 공격 상태. 진입 즉시 1타를 가하고 MinAttackPeriod 주기로 반복한다.
/// 사거리를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class MonsterAttackState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;
    private float cooldown;
    private float animTimer; // 공격 클립 남은 시간 (끝나면 대기 자세로)

    public MonsterAttackState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    // 교전 상태 전체가 아니라 실제 공격/시전 모션 중일 때만 Hit를 막는다. (쿨다운·접근 중에는 Hit로 끊긴다)
    public override bool IsAttackState => Owner.IsInAttackMotion;

    public override void OnEnter()
    {
        Owner.PlayAnim(MonsterAnimId.Attack);
        Owner.PerformAttack();              // 진입 즉시 1타
        cooldown = Owner.NextMeleeDelay(Owner.AttackAnimHoldTime, Owner.MinAttackPeriod);
        animTimer = Owner.AttackAnimHoldTime;
    }

    public override void Update(float time = 1.0f)
    {
        if (!Owner.IsTargetValid())
        {
            Owner.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 사거리를 벗어나면 다시 추적
        if (!Owner.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        Owner.FaceTargetStep(time);

        cooldown -= time;

        // 공격 클립이 끝나면 대기 자세로 (다음 공격 때 클립이 처음부터 다시 재생되도록)
        if (animTimer > 0f)
        {
            animTimer -= time;
            if (animTimer <= 0f && Owner.CurrentAnimId == MonsterAnimId.Attack)
                Owner.PlayAnim(MonsterAnimId.Idle);
        }

        if (cooldown <= 0f)
        {
            Owner.PlayAnim(MonsterAnimId.Attack);
            Owner.PerformAttack();
            cooldown = Owner.NextMeleeDelay(Owner.AttackAnimHoldTime, Owner.MinAttackPeriod);
            animTimer = Owner.AttackAnimHoldTime;
        }
    }
}

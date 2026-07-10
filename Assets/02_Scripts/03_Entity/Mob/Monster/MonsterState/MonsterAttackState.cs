/// <summary>
/// 공격 상태. 진입 즉시 1타를 가하고 MinAttackPeriod 주기로 반복한다.
/// 사거리를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class MonsterAttackState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;
    private float cooldown;

    public MonsterAttackState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.PlayAnim(Owner.AttackAnim);
        Owner.PerformAttack();              // 진입 즉시 1타
        cooldown = Owner.MinAttackPeriod;
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
        if (cooldown <= 0f)
        {
            Owner.PlayAnim(Owner.AttackAnim);
            Owner.PerformAttack();
            cooldown = Owner.MinAttackPeriod;
        }
    }
}

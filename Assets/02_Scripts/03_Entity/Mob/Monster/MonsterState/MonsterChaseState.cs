/// <summary>
/// 추적 상태. 타깃을 향해 이동하며, 공격 사거리 진입 시 Attack, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class MonsterChaseState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;

    public MonsterChaseState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    // chaseBool이 없는 몬스터는 Move와 같은 Bool로 매핑되므로 기존과 동일하게 재생된다.
    public override void OnEnter()
        => Owner.PlayAnim(MonsterAnimId.Chase);

    public override void Update(float time = 1.0f)
    {
        // 타깃이 사라졌거나 DetectRange를 벗어나면 추적 포기
        if (!Owner.IsTargetValid())
        {
            Owner.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 사거리 안이면 공격으로
        if (Owner.IsTargetInAttackRange())
        {
            sm.ToAttack();
            return;
        }

        Owner.ChaseStep(time);
    }
}

/// <summary>
/// Monster 전용 상태 머신. 상태 생성·전환 로직을 Monster 본체에서 분리해 이 클래스가 전담한다.
/// </summary>
public class MonsterStateMachine : StateMachine<MobState<Monster>>
{
    private readonly Monster owner;

    public MonsterStateMachine(Monster owner)
    {
        this.owner = owner;
        Init(new MonsterIdleState(owner, this)); // 초기 상태
    }

    public void ToIdle()
        => ChangeState(new MonsterIdleState(owner, this));

    public void ToChase()
        => ChangeState(new MonsterChaseState(owner, this));

    public void ToAttack()
        => ChangeState(new MonsterAttackState(owner, this));

    public void ToDead()
        => ChangeState(new MobDeadState<Monster>(owner, this));
}

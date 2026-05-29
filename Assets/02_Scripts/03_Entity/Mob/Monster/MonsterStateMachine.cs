/// <summary>
/// Monster 전용 상태 머신. 상태 생성·전환 로직을 Monster 본체에서 분리해 이 클래스가 전담한다.
/// </summary>
public class MonsterStateMachine : StateMachine<MobState<Monster>>
{
    private readonly Monster owner;

    public MonsterStateMachine(Monster owner)
    {
        this.owner = owner;
        Init(new MobIdleState<Monster>(owner, this)); // 초기 상태
    }

    public void ToDead()
        => ChangeState(new MobDeadState<Monster>(owner, this));

    // TODO: ToChase() / ToAttack() 등 몬스터 전용 전환 추가
}

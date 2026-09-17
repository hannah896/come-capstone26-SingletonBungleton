/// <summary>
/// Animal 전용 상태 머신. 상태 생성·전환 로직을 Animal 본체에서 분리해 이 클래스가 전담한다.
/// 흐름: Idle(대기) ⇄ Wander(배회) / 피격 → Knockback(넉백) → 착지 → Flee(도망) → 플레이어 이탈 → Idle
/// </summary>
public class AnimalStateMachine : StateMachine<MobState<Animal>>
{
    private readonly Animal owner;

    public AnimalStateMachine(Animal owner)
    {
        this.owner = owner;
        Init(new AnimalIdleState(owner, this)); // 초기 상태
    }

    public void ToIdle()
        => ChangeState(new AnimalIdleState(owner, this));

    public void ToWander()
        => ChangeState(new AnimalWanderState(owner, this));

    public void ToKnockback()
        => ChangeState(new AnimalKnockbackState(owner, this));

    public void ToFlee()
        => ChangeState(new AnimalFleeState(owner, this));

    public void ToDead()
        => ChangeState(new MobDeadState<Animal>(owner, this));
}

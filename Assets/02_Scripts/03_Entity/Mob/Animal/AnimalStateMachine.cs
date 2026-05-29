/// <summary>
/// Animal 전용 상태 머신. 상태 생성·전환 로직을 Animal 본체에서 분리해 이 클래스가 전담한다.
/// </summary>
public class AnimalStateMachine : StateMachine<MobState<Animal>>
{
    private readonly Animal owner;

    public AnimalStateMachine(Animal owner)
    {
        this.owner = owner;
        Init(new MobIdleState<Animal>(owner, this)); // 초기 상태
    }

    public void ToDead()
        => ChangeState(new MobDeadState<Animal>(owner, this));

    // TODO: ToWander() / ToFlee() 등 동물 전용 전환 추가
}

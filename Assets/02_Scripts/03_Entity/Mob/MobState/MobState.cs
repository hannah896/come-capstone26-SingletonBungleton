/// <summary>
/// Mob 일반 FSM의 상태 베이스(제네릭). TMob으로 소유 엔티티 타입을 고정해
/// 캐스팅 없이 Monster/Animal 고유 데이터에 접근한다.
/// </summary>
public abstract class MobState<TMob> : StateBase where TMob : Mob
{
    protected TMob Owner { get; }
    protected StateMachine<MobState<TMob>> Machine { get; }

    protected MobState(TMob owner, StateMachine<MobState<TMob>> machine)
    {
        Owner = owner;
        Machine = machine;
    }

    public override void OnEnter() { }
    public override void OnExit() { }
    public override void Update(float time = 1.0f) { }
    public override void FixedUpdate(float time = 1.0f) { }
}

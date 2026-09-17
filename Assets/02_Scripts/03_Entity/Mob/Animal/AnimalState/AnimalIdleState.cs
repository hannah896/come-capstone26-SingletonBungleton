/// <summary>
/// 대기 상태. IdleDuration(기본 5초)만큼 제자리에 있다가 배회(Wander)로 전환한다.
/// </summary>
public class AnimalIdleState : MobState<Animal>
{
    private readonly AnimalStateMachine sm;
    private float remaining;

    public AnimalIdleState(Animal owner, AnimalStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.StopMoving();
        Owner.PlayAnim(AnimalAnimId.Idle);
        remaining = Owner.IdleDuration;
    }

    public override void Update(float time = 1.0f)
    {
        remaining -= time;
        if (remaining > 0f) return;

        sm.ToWander();
    }
}

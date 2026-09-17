/// <summary>
/// 배회 상태. 주변 NavMesh 위 랜덤 지점을 찍어 걸어가고, 도착하면 Idle로 복귀한다.
/// 목적지를 못 찾거나 NavMesh에 아직 붙지 못했으면 바로 Idle로 돌아가 다음 주기에 다시 시도한다.
/// </summary>
public class AnimalWanderState : MobState<Animal>
{
    private readonly AnimalStateMachine sm;
    private bool started;

    public AnimalWanderState(Animal owner, AnimalStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        // OnEnter 안에서 곧바로 ChangeState하지 않도록 결과만 기록하고 전환은 Update에서 한다.
        started = Owner.TryGetWanderPoint(out var point) && Owner.MoveTo(point, Owner.WalkSpeed);
        if (started)
            Owner.PlayAnim(AnimalAnimId.Walk);
    }

    public override void Update(float time = 1.0f)
    {
        if (!started || Owner.HasArrived())
            sm.ToIdle();
    }

    public override void OnExit()
        => Owner.StopMoving();
}

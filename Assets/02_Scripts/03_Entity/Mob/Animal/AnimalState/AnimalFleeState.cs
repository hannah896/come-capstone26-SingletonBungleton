/// <summary>
/// 도망 상태. FleeRange 안에 플레이어가 있는 동안 반대 방향으로 뛰어가고,
/// 범위 안에 플레이어가 하나도 없으면 Idle로 복귀한다.
/// </summary>
public class AnimalFleeState : MobState<Animal>
{
    private readonly AnimalStateMachine sm;
    private float repathTimer;

    public AnimalFleeState(Animal owner, AnimalStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.PlayAnim(AnimalAnimId.Run);
        repathTimer = 0f; // 진입 즉시 첫 목적지 계산
    }

    public override void Update(float time = 1.0f)
    {
        // 주변에 플레이어가 없으면 도망 종료
        if (!Owner.TryGetThreatPosition(out var threatPosition))
        {
            sm.ToIdle();
            return;
        }

        repathTimer -= time;
        if (repathTimer > 0f && !Owner.HasArrived()) return;

        repathTimer = Owner.FleeRepathInterval;
        if (Owner.TryGetFleePoint(threatPosition, out var point))
            Owner.MoveTo(point, Owner.RunSpeed);
    }

    public override void OnExit()
        => Owner.StopMoving();
}

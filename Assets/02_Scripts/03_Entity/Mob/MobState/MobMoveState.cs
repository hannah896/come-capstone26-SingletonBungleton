/// <summary>
/// 이동 상태 골격. 목적지로 이동하며, 도착·정지 시 Idle로 복귀.
/// </summary>
public class MobMoveState<TMob> : MobState<TMob> where TMob : Mob
{
    public MobMoveState(TMob owner, StateMachine<MobState<TMob>> machine) : base(owner, machine) { }

    public override void OnEnter()
    {
        // TODO: Move 애니메이션 트리거
    }

    public override void Update(float time = 1.0f)
    {
        // TODO: MoveSpeed 기반 이동, 도착 시 Machine.ChangeState(new MobIdleState<TMob>(Owner, Machine))
    }
}

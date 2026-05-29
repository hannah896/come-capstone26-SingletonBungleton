/// <summary>
/// 대기 상태 골격. 추후 인지 범위 내 타깃 감지 시 이동/추적/도망 상태로 전환.
/// </summary>
public class MobIdleState<TMob> : MobState<TMob> where TMob : Mob
{
    public MobIdleState(TMob owner, StateMachine<MobState<TMob>> machine) : base(owner, machine) { }

    public override void OnEnter()
    {
        // TODO: Idle 애니메이션 트리거
    }

    public override void Update(float time = 1.0f)
    {
        // TODO: 타깃 감지 시 Machine.ChangeState(new MobMoveState<TMob>(Owner, Machine)) 등
    }
}

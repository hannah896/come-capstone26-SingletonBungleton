/// <summary>
/// 사망 상태 골격. 진입 시 사망 연출을 재생하고 더 이상 입력/전환을 받지 않는다.
/// 드랍 스폰과 Despawn은 Mob.HandleDead에서 처리한다.
/// </summary>
public class MobDeadState<TMob> : MobState<TMob> where TMob : Mob
{
    public MobDeadState(TMob owner, StateMachine<MobState<TMob>> machine) : base(owner, machine) { }

    public override void OnEnter()
    {
        // TODO: Dead 애니메이션 트리거
    }
}

/// <summary>
/// 사망 상태. 진입 시 사망 연출을 재생하고 더 이상 입력/전환을 받지 않는다.
/// 연출이 끝나는 시점(사망 클립의 Animation Event → Mob.OnDeathAnimEnd)에 드랍 스폰과 Despawn이 일어난다.
/// </summary>
public class MobDeadState<TMob> : MobState<TMob> where TMob : Mob
{
    public MobDeadState(TMob owner, StateMachine<MobState<TMob>> machine) : base(owner, machine) { }

    public override void OnEnter()
        => Owner.PlayDeadAnim();
}

/// <summary>
/// 대기 상태. 매 프레임 DetectRange/FOV로 플레이어를 탐색하고, 발견 시 Chase로 전환한다.
/// </summary>
public class MonsterIdleState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;

    public MonsterIdleState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
        => Owner.PlayAnim(MonsterAnimId.Idle);

    public override void Update(float time = 1.0f)
    {
        if (Owner.AcquireTarget())
            sm.ToChase();
    }
}

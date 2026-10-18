/// <summary>
/// 진짜를 들켰다. 분신이 모두 사라진 뒤 CloneRevealStunTime 동안 비틀거린다(피격 모션) — 플레이어의 딜 타임.
/// 이 동안은 다시 경직으로 끊기지 않는다.
/// </summary>
public class DevilCloneRevealState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;
    private float elapsed;

    public DevilCloneRevealState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        devil.PlayAnim(MonsterAnimId.Hit);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;
        if (elapsed < devil.CloneRevealStunTime) return;

        devil.PlayAnim(MonsterAnimId.Idle);
        sm.ToAttack();
    }
}

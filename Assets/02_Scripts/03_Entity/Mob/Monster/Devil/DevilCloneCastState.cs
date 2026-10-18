/// <summary>
/// 진짜 찾기 시전. 제자리에서 연기를 피우며 사라졌다가(CloneVanishTime),
/// 플레이어를 둘러싼 원 위에 진짜와 분신이 함께 나타난다(CloneAppearTime). 이후 지상 교전으로 돌아간다.
/// 시전 중에는 끊기지 않는다.
/// </summary>
public class DevilCloneCastState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;
    private float elapsed;
    private bool vanished;

    public DevilCloneCastState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        vanished = false;
        devil.ConsumeClonePending();
        devil.PlayAnim(MonsterAnimId.Idle);
        devil.PlayPoof(devil.transform.position);
    }

    public override void OnExit()
    {
        devil.SetHidden(false);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;
        devil.FaceTargetStep(time);

        if (!vanished && elapsed >= devil.CloneVanishTime)
        {
            vanished = true;
            devil.SetHidden(true);
        }

        if (elapsed < devil.CloneAppearTime) return;

        devil.BeginClonePhase();
        devil.SetHidden(false);
        sm.ToAttack();
    }
}

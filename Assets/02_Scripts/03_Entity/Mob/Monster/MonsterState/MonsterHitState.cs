/// <summary>
/// 피격 상태. 공격 중이 아닐 때 피격당하면 진입한다(공격 중 피격은 Monster.HandleDamaged에서 걸러짐).
/// Take Damage 애니메이션을 재생하고 HitDuration만큼 대기한 뒤 타깃 상태(Chase/Idle)로 복귀한다.
/// </summary>
public class MonsterHitState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;
    private float remaining;

    public MonsterHitState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.PlayAnim(MonsterAnimId.Hit);
        remaining = Owner.HitDuration;
    }

    public override void Update(float time = 1.0f)
    {
        remaining -= time;
        if (remaining > 0f) return;

        if (!Owner.IsTargetValid())
        {
            Owner.ClearTarget();
            sm.ToIdle();
            return;
        }

        sm.ToChase();
    }
}

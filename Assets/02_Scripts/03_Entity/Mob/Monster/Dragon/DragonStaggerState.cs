/// <summary>
/// 카운터 경직. 물기 선딜에 머리를 맞아 패턴이 끊긴 상태 (GetHit2).
/// 경직이 끝날 때 무력화 게이지가 바닥나 있으면 곧장 그로기로 넘어간다.
/// </summary>
public class DragonStaggerState : MobState<Monster>
{
    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private float elapsed;

    public DragonStaggerState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        boss.PlayAnim(MonsterAnimId.HitHeavy);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;
        var d = boss.Data;
        if (elapsed < (d != null ? d.CounterStaggerDuration : 1.6f)) return;

        if (boss.IsPoiseBroken && d != null)
        {
            sm.ToGroggy(d.GroggyDuration, d.GroggyDamageMultiplier);
            return;
        }

        boss.PlayAnim(MonsterAnimId.Idle);
        sm.ToAttack();
    }
}

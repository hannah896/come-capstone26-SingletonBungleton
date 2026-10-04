/// <summary>
/// 트리가드 페이즈 전환 연출. HP가 기준선 아래로 내려간 뒤, 하던 기술이 끝나면 들어온다.
/// Cast Spell을 재생하는 동안 무적이며, 쿨타임과 상관없이 숲의 부름을 그 페이즈 마릿수로 1회 쓴다.
/// </summary>
public class TreeguardPhaseShiftState : MobState<Monster>
{
    private readonly TreeguardStateMachine sm;
    private readonly Treeguard boss;
    private float elapsed;
    private float summonTime;
    private bool summoned;

    public TreeguardPhaseShiftState(Treeguard owner, TreeguardStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        summoned = false;
        summonTime = boss.SkillHitTime(TreeguardSkill.Summon);

        boss.ConsumePhaseShift();
        boss.SetInvulnerable(true);
        boss.PlayAnim(MonsterAnimId.CastSpell);
    }

    public override void OnExit()
    {
        boss.SetInvulnerable(false);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;

        if (!summoned && elapsed >= summonTime)
        {
            summoned = true;
            boss.SummonMinions();
        }

        if (elapsed < boss.PhaseShiftDuration) return;

        sm.ToAttack();
    }
}

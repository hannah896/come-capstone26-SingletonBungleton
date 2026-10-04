/// <summary>
/// 트리가드 공격(교전) 상태. 거리·방향·쿨타임으로 다음 기술을 고르고, 고를 게 없으면 천천히 다가온다.
///
/// 선택 순서 (조건을 만족하고 쿨이 돌아온 첫 기술):
/// ① 등 뒤 + 가까움        → 대지 울림 (등 뒤 공략의 대가)
/// ② 바로 앞에 붙음        → 밀쳐내기
/// ③ 내려찍기 거리         → 고목 내려찍기 (주력 빈틈기)
/// ④ 휩쓸기 거리           → 가지 휩쓸기
/// ⑤ 소환 미니언이 적음    → 숲의 부름
/// ⑥ 거리를 벌림           → 박수 충격파
/// ⑦ 멀리 떨어짐           → 가지 투척
/// ⑧ 전부 쿨               → 타깃 쪽으로 천천히 돌며 걷는다 (3페이즈는 뛴다)
///
/// 대지 울림·박수는 2페이즈부터 쓴다. 기술 사이에는 최소 간격(SkillGap)을 둬서 숨 쉴 틈을 준다.
/// 페이즈 전환이 대기 중이면 다른 무엇보다 먼저 전환 연출로 간다.
/// </summary>
public class TreeguardAttackState : MobState<Monster>
{
    private readonly TreeguardStateMachine sm;
    private readonly Treeguard boss;
    private float gapTimer;     // 기술 사이 최소 간격 남은 시간

    public TreeguardAttackState(Treeguard owner, TreeguardStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        gapTimer = boss.SkillGap;
    }

    public override void Update(float time = 1.0f)
    {
        if (!boss.IsTargetValid())
        {
            boss.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 페이즈 전환은 하던 기술이 끝난 뒤, 다음 판단의 맨 처음에 한다.
        if (boss.IsPhaseShiftPending)
        {
            sm.ToPhaseShift();
            return;
        }

        if (gapTimer > 0f)
        {
            gapTimer -= time;
            Approach(time);
            return;
        }

        if (TrySelectSkill(out TreeguardSkill skill))
        {
            sm.ToSkill(skill);
            return;
        }

        Approach(time);
    }

    // 4.3 선택 규칙. 쿨이 돌아온 첫 기술을 고른다.
    private bool TrySelectSkill(out TreeguardSkill skill)
    {
        var d = boss.Data;
        if (d == null)
        {
            skill = default;
            return false;
        }

        float dist = boss.TargetDistance;
        bool phase2 = boss.Phase >= 2;

        // ① 대지 울림: 등 뒤에 붙은 플레이어를 털어낸다
        if (phase2 && boss.IsSkillReady(TreeguardSkill.Step) && dist <= d.StepTriggerRange && boss.IsTargetBehind())
            return Pick(TreeguardSkill.Step, out skill);

        // ② 밀쳐내기: 바로 앞에 붙어 있으면 떼어낸다
        if (boss.IsSkillReady(TreeguardSkill.Kick) && dist <= d.KickTriggerRange && !boss.IsTargetBehind())
            return Pick(TreeguardSkill.Kick, out skill);

        // ③ 고목 내려찍기: 주력 빈틈기
        if (boss.IsSkillReady(TreeguardSkill.Smack) && dist <= d.SmackRange && !boss.IsTargetBehind())
            return Pick(TreeguardSkill.Smack, out skill);

        // ④ 가지 휩쓸기
        if (boss.IsSkillReady(TreeguardSkill.Swing) && dist <= d.SwingRange && !boss.IsTargetBehind())
            return Pick(TreeguardSkill.Swing, out skill);

        // ⑤ 숲의 부름: 소환 미니언이 적으면 부른다
        if (boss.IsSkillReady(TreeguardSkill.Summon) && boss.AliveMinionCount < d.SummonWhenFewerThan)
            return Pick(TreeguardSkill.Summon, out skill);

        // ⑥ 박수 충격파: 거리를 벌린 플레이어를 노린다 (정면일 때만, 직선 기술이라)
        if (phase2 && boss.IsSkillReady(TreeguardSkill.Clap) && dist >= d.ClapMinRange && !boss.IsTargetBehind())
            return Pick(TreeguardSkill.Clap, out skill);

        // ⑦ 가지 투척: 멀리서 견제하는 플레이어를 노린다
        if (boss.IsSkillReady(TreeguardSkill.Projectile) && dist >= d.ProjectileMinRange)
            return Pick(TreeguardSkill.Projectile, out skill);

        skill = default;
        return false;
    }

    private static bool Pick(TreeguardSkill picked, out TreeguardSkill skill)
    {
        skill = picked;
        return true;
    }

    // ⑧ 기술이 없으면 휩쓸기 거리까지 다가간다. 이미 가까우면 제자리에서 몸만 돌린다.
    private void Approach(float time)
    {
        float stopRange = boss.Data != null ? boss.Data.KickTriggerRange : 1.8f;
        if (boss.TargetDistance > stopRange)
        {
            boss.MoveStep(time);
        }
        else
        {
            boss.PlayAnim(MonsterAnimId.Idle);
            boss.FaceTargetStep(time);
        }
    }
}

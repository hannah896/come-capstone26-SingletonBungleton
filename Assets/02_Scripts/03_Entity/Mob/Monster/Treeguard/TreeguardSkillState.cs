/// <summary>
/// 트리가드 기술 1회 진행 상태. 모든 기술이 같은 흐름을 따르므로 기술 종류만 바꿔 재사용한다.
///
/// ① 진입: 방향을 고정한다(이후로 돌지 않는다). 쿨타임을 걸고 클립을 재생하며, 장판 기술은 이때 예고 장판을 깐다.
/// ② 타격 시점: 부채꼴 근접기는 판정, 숲의 부름은 소환. (장판 기술은 예고 시간이 끝나면 장판이 알아서 터진다)
/// ③ 클립이 끝나면 Idle로 서서 빈틈을 준다. 내려찍기만은 빈틈 내내 팔이 땅에 박힌 자세를 유지한다.
/// ④ 전체 시간이 지나면 공격 상태로 돌아가 다음 기술을 고른다.
///    3페이즈 내려찍기는 확률로 휩쓸기를 바로 이어 쓰고, 연계 휩쓸기는 빈틈이 더 길다.
///
/// 기술 중에는 그로기 외에는 끊기지 않는다(슈퍼아머).
/// </summary>
public class TreeguardSkillState : MobState<Monster>
{
    private readonly TreeguardStateMachine sm;
    private readonly Treeguard boss;
    private readonly TreeguardSkill skill;
    private readonly bool combo;

    private float elapsed;
    private float hitTime;
    private float animHoldTime;
    private float duration;
    private bool hitDone;
    private bool idleSet;

    public TreeguardSkillState(Treeguard owner, TreeguardStateMachine machine, TreeguardSkill skill, bool combo)
        : base(owner, machine)
    {
        boss = owner;
        sm = machine;
        this.skill = skill;
        this.combo = combo;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        hitDone = false;
        idleSet = false;
        hitTime = boss.SkillHitTime(skill);
        animHoldTime = boss.SkillAnimHoldTime(skill, combo);
        duration = boss.SkillDuration(skill, combo);

        // 연계 휩쓸기는 내려찍기 자세에서 곧장 몸을 돌려 휘두르므로 정렬 없이 바로 시작한다.
        boss.BeginSkill(skill);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;

        if (!hitDone && elapsed >= hitTime)
        {
            hitDone = true;
            boss.SkillHit(skill);
        }

        if (!idleSet && elapsed >= animHoldTime)
        {
            idleSet = true;
            boss.PlayAnim(MonsterAnimId.Idle);
        }

        if (elapsed < duration) return;

        // 3페이즈 연계: 내려찍기 → 휩쓸기
        if (skill == TreeguardSkill.Smack && !combo && boss.IsTargetValid() && boss.RollPhase3Combo())
        {
            sm.ToSkill(TreeguardSkill.Swing, combo: true);
            return;
        }

        sm.ToAttack();
    }
}

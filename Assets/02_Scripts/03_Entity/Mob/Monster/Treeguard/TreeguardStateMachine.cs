/// <summary>
/// 트리가드 전용 상태 머신.
/// - 공격 상태를 TreeguardAttackState(기술 선택 + 접근)로 교체한다. 추적도 공격 상태가 직접 하므로 Chase는 공격 상태로 보낸다.
/// - 피격 경직(Hit)은 없다(슈퍼아머). 대신 강인도가 깨지면 그로기 상태로 간다.
/// - 기술 1회 진행(TreeguardSkillState), 그로기(TreeguardGroggyState), 페이즈 전환(TreeguardPhaseShiftState)을 더한다.
/// Spawn/Idle/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class TreeguardStateMachine : MonsterStateMachine
{
    private readonly Treeguard boss;

    public TreeguardStateMachine(Treeguard owner) : base(owner)
    {
        boss = owner;
    }

    public override void ToAttack()
        => ChangeState(new TreeguardAttackState(boss, this));

    // 공격 상태가 교전 사거리 밖에서도 직접 걸어 들어오므로 별도 추적 상태를 쓰지 않는다.
    public override void ToChase()
        => ToAttack();

    // 슈퍼아머: 맞아도 움찔하지 않는다. (강인도 → 그로기가 대신한다)
    public override void ToHit() { }

    /// <summary>기술 하나를 처음부터 끝까지 진행한다. combo는 3페이즈 내려찍기 → 휩쓸기 연계.</summary>
    public void ToSkill(TreeguardSkill skill, bool combo = false)
        => ChangeState(new TreeguardSkillState(boss, this, skill, combo));

    /// <summary>강인도가 깨져 무방비가 된다.</summary>
    public void ToGroggy()
        => ChangeState(new TreeguardGroggyState(boss, this));

    /// <summary>페이즈 전환 연출 (Cast Spell + 무적 + 숲의 부름).</summary>
    public void ToPhaseShift()
        => ChangeState(new TreeguardPhaseShiftState(boss, this));
}

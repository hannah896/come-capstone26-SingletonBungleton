/// <summary>
/// Demon 전용 상태 머신. 공격 상태만 DemonAttackState(슬래시/운석 세례 선택)로 교체한다.
/// Idle/Chase/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class DemonStateMachine : MonsterStateMachine
{
    private readonly Demon demon;

    public DemonStateMachine(Demon owner) : base(owner)
    {
        demon = owner;
    }

    public override void ToAttack()
        => ChangeState(new DemonAttackState(demon, this));

    /// <summary>타깃을 향해 도약한다(스크립트 이동, 콜라이더 함께 상승).</summary>
    public void ToLeap()
        => ChangeState(new DemonLeapState(demon, this));
}

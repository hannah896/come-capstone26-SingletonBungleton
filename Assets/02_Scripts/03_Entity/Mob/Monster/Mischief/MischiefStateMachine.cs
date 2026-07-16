/// <summary>
/// Mischief 전용 상태 머신. 공격 상태만 MischiefAttackState(슬래시/프로젝타일 선택)로 교체한다.
/// Idle/Chase/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class MischiefStateMachine : MonsterStateMachine
{
    private readonly Mischief mischief;

    public MischiefStateMachine(Mischief owner) : base(owner)
    {
        mischief = owner;
    }

    public override void ToAttack()
        => ChangeState(new MischiefAttackState(mischief, this));

    /// <summary>타깃을 향해 도약한다(스크립트 이동, 콜라이더 함께 상승).</summary>
    public void ToLeap()
        => ChangeState(new MischiefLeapState(mischief, this));
}

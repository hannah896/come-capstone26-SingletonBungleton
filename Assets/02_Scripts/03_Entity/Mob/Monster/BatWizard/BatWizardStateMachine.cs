/// <summary>
/// 외눈 박쥐 위저드 전용 상태 머신. 공격 상태만 BatWizardAttackState(마법탄/스펠/근접/스핀 선택·실행)로 교체한다.
/// Spawn/Idle/Wander/Chase/Hit/Dead는 베이스 Monster 상태를 그대로 사용한다. (일반 몬스터라 경직·넉백 있음)
/// </summary>
public class BatWizardStateMachine : MonsterStateMachine
{
    private readonly BatWizard wizard;

    public BatWizardStateMachine(BatWizard owner) : base(owner)
    {
        wizard = owner;
    }

    public override void ToAttack()
        => ChangeState(new BatWizardAttackState(wizard, this));
}

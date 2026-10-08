/// <summary>
/// 외눈 박쥐 메이지 전용 상태 머신.
/// - 공격 상태를 BatMageAttackState(슬래시/스핀/스펠 선택·실행)로 교체한다
/// - 피격 경직은 StaggerCooldown 간격으로만 건다 (보스라 매 타격마다 끊기지 않게)
/// Spawn/Idle/Wander/Chase/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class BatMageStateMachine : MonsterStateMachine
{
    private readonly BatMage mage;

    public BatMageStateMachine(BatMage owner) : base(owner)
    {
        mage = owner;
    }

    public override void ToAttack()
        => ChangeState(new BatMageAttackState(mage, this));

    public override void ToHit()
    {
        if (mage.TryConsumeStagger())
            base.ToHit();
    }
}

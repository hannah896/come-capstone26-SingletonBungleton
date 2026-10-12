/// <summary>
/// 외눈 박쥐 전용 상태 머신. 공격 상태만 BatAttackState(물기/눈알 탄 선택)로 교체한다.
/// Spawn/Idle/Wander/Chase/Hit/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class BatStateMachine : MonsterStateMachine
{
    private readonly Bat bat;

    public BatStateMachine(Bat owner) : base(owner)
    {
        bat = owner;
    }

    public override void ToAttack()
        => ChangeState(new BatAttackState(bat, this));
}

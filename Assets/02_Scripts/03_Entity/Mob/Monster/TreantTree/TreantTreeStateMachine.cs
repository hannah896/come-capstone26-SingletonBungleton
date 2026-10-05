/// <summary>
/// 트리앤트 트리 전용 상태 머신. 공격 상태만 TreantTreeAttackState(주먹/투척 선택)로 교체한다.
/// Spawn/Idle/Chase/Hit/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class TreantTreeStateMachine : MonsterStateMachine
{
    private readonly TreantTree treant;

    public TreantTreeStateMachine(TreantTree owner) : base(owner)
    {
        treant = owner;
    }

    public override void ToAttack()
        => ChangeState(new TreantTreeAttackState(treant, this));
}

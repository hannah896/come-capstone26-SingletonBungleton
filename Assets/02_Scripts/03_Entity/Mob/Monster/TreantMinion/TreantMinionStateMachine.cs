/// <summary>
/// 트리앤트 미니언 전용 상태 머신. 공격 상태를 TreantMinionAttackState(세 기술 선택)로 교체하고,
/// 구르기 돌진을 위한 전용 상태(TreantMinionRollState)를 더한다.
/// Idle/Chase/Hit/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class TreantMinionStateMachine : MonsterStateMachine
{
    private readonly TreantMinion treant;

    public TreantMinionStateMachine(TreantMinion owner) : base(owner)
    {
        treant = owner;
    }

    public override void ToAttack()
        => ChangeState(new TreantMinionAttackState(treant, this));

    /// <summary>타깃을 향해 굴러 들이받는다(스크립트 직선 이동 + 통과 판정).</summary>
    public void ToRoll()
        => ChangeState(new TreantMinionRollState(treant, this));
}

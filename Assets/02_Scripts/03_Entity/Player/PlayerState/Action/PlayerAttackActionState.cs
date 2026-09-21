using UnityEngine;

/// <summary>
/// 전투 공격 액션. 손에 든 무기/도구로 몬스터·동물을 친다.
///
/// 애니메이터에 전용 공격 클립이 아직 없어서 벌목 스윙(Action_Chop_Begin → Loop → Stop, 2.800s)을 그대로 빌려 쓴다.
/// 타격 시점도 벌목과 같은 1.333s. 공격 클립이 생기면 ActionTrigger / LoopStateHash / Swing* 구간만 바꾸면 된다.
/// (LoopStateHash를 바꾸면 PlayerAnimData.s_crossFadeStates에도 추가할 것)
/// </summary>
public class PlayerAttackActionState : PlayerActionSubStateBase
{
    private static readonly int s_beginStateHash = Animator.StringToHash("Action_Chop_Begin");

    public PlayerAttackActionState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override int LoopStateHash => s_beginStateHash;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;

    protected override float SwingRiseDuration => 1.233f;    // Begin 0.500 + Loop 최고점 0.733
    protected override float SwingStrikeDuration => 0.100f;  // Loop 0.733 → 0.833
    protected override float SwingRecoverDuration => 1.467f; // 타격 1.333 → 액션 종료 2.800
}

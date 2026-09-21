using UnityEngine;

/// <summary>
/// 벌목 액션. 애니메이터 체인은 Action_Chop_Begin(0.500s) → Loop(1.733s) → Stop(0.567s) = 2.800s.
/// Loop 클립에서 도끼가 0.733s에 최고점을 찍고 0.833s에 내리꽂히므로, 실제 타격은 액션 시작 1.333s 시점이다.
/// </summary>
public class PlayerChopState : PlayerActionSubStateBase
{
    private static readonly int s_beginStateHash = Animator.StringToHash("Action_Chop_Begin");

    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override int LoopStateHash => s_beginStateHash;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;

    protected override float SwingRiseDuration => 1.233f;    // Begin 0.500 + Loop 최고점 0.733
    protected override float SwingStrikeDuration => 0.100f;  // Loop 0.733 → 0.833
    protected override float SwingRecoverDuration => 1.467f; // 타격 1.333 → 액션 종료 2.800
}

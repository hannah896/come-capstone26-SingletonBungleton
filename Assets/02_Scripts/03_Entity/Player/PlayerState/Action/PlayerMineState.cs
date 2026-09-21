using UnityEngine;

/// <summary>
/// 채굴 액션. 애니메이터 체인은 Action_Mine_Begin(0.500s) → Loop(1.667s) → Stop(0.567s) = 2.734s.
/// Loop 클립의 최고점 0.733s, 타격 0.833s로 벌목과 같아 타격 시점은 액션 시작 1.333s다.
/// </summary>
public class PlayerMineState : PlayerActionSubStateBase
{
    private static readonly int s_beginStateHash = Animator.StringToHash("Action_Mine_Begin");

    public PlayerMineState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Mine;
    protected override int LoopStateHash => s_beginStateHash;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;

    protected override float SwingRiseDuration => 1.233f;    // Begin 0.500 + Loop 최고점 0.733
    protected override float SwingStrikeDuration => 0.100f;  // Loop 0.733 → 0.833
    protected override float SwingRecoverDuration => 1.401f; // 타격 1.333 → 액션 종료 2.734
}

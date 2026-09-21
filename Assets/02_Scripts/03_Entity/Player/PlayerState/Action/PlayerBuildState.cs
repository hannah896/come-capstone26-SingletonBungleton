/// <summary>
/// 망치질(구조물 철거) 액션. 애니메이터 체인은 Action_Build_Begin(0.500s) → Loop(0.933s) → Stop(0.500s) = 1.933s.
/// 이 클립들도 FBX 내장이라 Animation Event가 없어 타이머로 타격/종료를 판단한다.
/// </summary>
public class PlayerBuildState : PlayerActionSubStateBase
{
    public PlayerBuildState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Build;
    protected override bool UsesToolOnComplete => true;

    protected override float SwingRiseDuration => 0.848f;    // Begin 0.500 + Loop 0.348
    protected override float SwingStrikeDuration => 0.100f;  // Loop 0.348 → 0.448
    protected override float SwingRecoverDuration => 0.985f; // 타격 0.948 → 액션 종료 1.933
}

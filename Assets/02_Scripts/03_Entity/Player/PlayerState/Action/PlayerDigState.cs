/// <summary>
/// 땅파기 액션. 애니메이터 체인은 Action_Dig_Begin(0.667s) → Loop(1.567s) → Stop(0.667s) = 2.901s.
/// 이 클립들은 FBX 내장이라 Animation Event가 없어 종료도 타이머가 판단한다.
/// 타격 시점은 벌목/채굴에서 측정한 비율(Loop 길이의 약 48%)을 적용했다.
/// </summary>
public class PlayerDigState : PlayerActionSubStateBase
{
    public PlayerDigState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Dig;
    protected override bool UsesToolOnComplete => true;

    protected override float SwingRiseDuration => 1.319f;    // Begin 0.667 + Loop 0.652
    protected override float SwingStrikeDuration => 0.100f;  // Loop 0.652 → 0.752
    protected override float SwingRecoverDuration => 1.482f; // 타격 1.419 → 액션 종료 2.901
}

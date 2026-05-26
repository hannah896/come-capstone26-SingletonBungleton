public class PlayerChopState : PlayerActionSubStateBase
{
    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override bool UsesToolOnComplete => true;

    // 도구 스윙은 OnEnter가 아니라 바디 chop 클립의 Animation Event(PlayerAnimEventRelay.PlayerToolUse)로 구동된다.
}

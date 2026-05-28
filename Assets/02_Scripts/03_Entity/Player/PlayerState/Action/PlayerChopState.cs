public class PlayerChopState : PlayerActionSubStateBase
{
    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Chop;
    protected override bool UsesToolOnComplete => true;
    protected override bool ShouldLoop => true;
}

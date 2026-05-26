public class PlayerDigState : PlayerActionSubStateBase
{
    public PlayerDigState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Dig;
    protected override bool UsesToolOnComplete => true;
}

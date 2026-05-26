public class PlayerIgniteState : PlayerActionSubStateBase
{
    public PlayerIgniteState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Ignite;
}

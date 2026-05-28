public class PlayerPickState : PlayerActionSubStateBase
{
    public PlayerPickState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Pick;
}

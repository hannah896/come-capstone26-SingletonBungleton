public class PlayerCookState : PlayerActionSubStateBase
{
    public PlayerCookState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Cook;
}

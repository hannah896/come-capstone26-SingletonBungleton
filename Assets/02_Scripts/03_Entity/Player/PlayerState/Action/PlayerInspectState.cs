public class PlayerInspectState : PlayerActionSubStateBase
{
    public PlayerInspectState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Inspect;
}

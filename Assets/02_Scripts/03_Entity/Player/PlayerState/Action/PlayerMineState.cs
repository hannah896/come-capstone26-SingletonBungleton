public class PlayerMineState : PlayerActionSubStateBase
{
    public PlayerMineState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Mine;
    protected override bool UsesToolOnComplete => true;
}

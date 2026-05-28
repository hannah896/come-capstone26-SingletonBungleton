public class PlayerBuildState : PlayerActionSubStateBase
{
    public PlayerBuildState(PlayerRootStateMachine machine) : base(machine) { }

    protected override int ActionTrigger => Machine.AnimData.AnimHashKey.Build;
}

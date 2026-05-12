using UnityEngine;

public class PlayerIgniteState : PlayerSubStateBase
{
    public PlayerIgniteState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Ignite);
        Debug.Log("[State] Ignite Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Ignite);
        Debug.Log("[State] Ignite Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

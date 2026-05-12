using UnityEngine;

public class PlayerPickState : PlayerSubStateBase
{
    public PlayerPickState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Pick);
        Debug.Log("[State] Pick Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Pick);
        Debug.Log("[State] Pick Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

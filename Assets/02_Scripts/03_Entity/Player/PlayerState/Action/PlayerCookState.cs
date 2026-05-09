using UnityEngine;

public class PlayerCookState : PlayerSubStateBase
{
    public PlayerCookState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Cook);
        Debug.Log("[State] Cook Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Cook);
        Debug.Log("[State] Cook Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

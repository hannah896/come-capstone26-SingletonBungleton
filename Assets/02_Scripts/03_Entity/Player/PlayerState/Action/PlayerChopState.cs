using UnityEngine;

public class PlayerChopState : PlayerSubStateBase
{
    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Chop);
        Entity.FPCameraController?.PlayChopSwing();
        Debug.Log("[State] Chop Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Chop);
        Debug.Log("[State] Chop Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
        {
            Entity.Inventory?.UseEquippedHandTool();
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
        }
    }
}

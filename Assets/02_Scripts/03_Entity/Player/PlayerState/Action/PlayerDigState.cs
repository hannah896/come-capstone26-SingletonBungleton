using UnityEngine;

public class PlayerDigState : PlayerSubStateBase
{
    public PlayerDigState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Dig);
        Debug.Log("[State] Dig Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Dig);
        Debug.Log("[State] Dig Exit");
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

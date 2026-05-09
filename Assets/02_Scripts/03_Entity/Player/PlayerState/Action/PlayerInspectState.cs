using UnityEngine;

public class PlayerInspectState : PlayerSubStateBase
{
    public PlayerInspectState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Inspect);
        Debug.Log("[State] Inspect Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Inspect);
        Debug.Log("[State] Inspect Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

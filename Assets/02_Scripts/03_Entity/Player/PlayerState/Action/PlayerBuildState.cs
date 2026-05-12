using UnityEngine;

public class PlayerBuildState : PlayerSubStateBase
{
    public PlayerBuildState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Build);
        Debug.Log("[State] Build Enter");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Build);
        Debug.Log("[State] Build Exit");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

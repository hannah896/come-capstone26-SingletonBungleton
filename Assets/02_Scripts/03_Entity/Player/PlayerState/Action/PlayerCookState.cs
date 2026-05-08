using UnityEngine;

/// <summary>
/// 요리 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@Watering01_R (Watering/Work)
/// </summary>
public class PlayerCookState : PlayerSubStateBase
{
    public PlayerCookState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Cook);
        Debug.Log("[State] Cook 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Cook);
        Debug.Log("[State] Cook 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

using UnityEngine;

/// <summary>
/// 채집 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@Gathering01 (Gathering)
/// </summary>
public class PlayerPickState : PlayerSubStateBase
{
    public PlayerPickState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Pick);
        Debug.Log("[State] Pick 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Pick);
        Debug.Log("[State] Pick 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

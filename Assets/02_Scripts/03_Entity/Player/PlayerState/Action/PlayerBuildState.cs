using UnityEngine;

/// <summary>
/// 설치 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@HammeringWall01_R - Begin/Loop/Stop (Hammering)
/// </summary>
public class PlayerBuildState : PlayerSubStateBase
{
    public PlayerBuildState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Build);
        Debug.Log("[State] Build 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Build);
        Debug.Log("[State] Build 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

using UnityEngine;

/// <summary>
/// 채굴 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@Mining - Begin/Loop Wall/Stop (Mining)
/// </summary>
public class PlayerMineState : PlayerSubStateBase
{
    public PlayerMineState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Mine);
        Debug.Log("[State] Mine 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Mine);
        Debug.Log("[State] Mine 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

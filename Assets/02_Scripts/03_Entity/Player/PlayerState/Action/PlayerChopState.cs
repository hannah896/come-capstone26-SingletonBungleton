using UnityEngine;

/// <summary>
/// 벌목 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@TreeChopping - Begin/Loop/Stop (Chopping)
/// </summary>
public class PlayerChopState : PlayerSubStateBase
{
    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Chop);
        Debug.Log("[State] Chop 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Chop);
        Debug.Log("[State] Chop 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

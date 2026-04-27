using UnityEngine;

/// <summary>
/// 뽑기 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@FarmingWithPlow01_R - Begin/Loop/Stop (Farming)
/// </summary>
public class PlayerDigState : PlayerSubStateBase
{
    public PlayerDigState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Dig);
        Debug.Log("[State] Dig 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Dig);
        Debug.Log("[State] Dig 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

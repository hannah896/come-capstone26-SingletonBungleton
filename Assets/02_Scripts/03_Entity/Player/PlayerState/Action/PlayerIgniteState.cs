using UnityEngine;

/// <summary>
/// 점화 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@Opening01 - Begin/Loop/Stop (Open/Misc)
/// </summary>
public class PlayerIgniteState : PlayerSubStateBase
{
    public PlayerIgniteState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Ignite);
        Debug.Log("[State] Ignite 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Ignite);
        Debug.Log("[State] Ignite 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

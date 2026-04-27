using UnityEngine;

/// <summary>
/// 조사 상태 (Sub, Action 하위)
/// Kevin Iglesias: HumanM@Loot01 - Begin/Loop/Stop (Loot/Misc)
/// </summary>
public class PlayerInspectState : PlayerSubStateBase
{
    public PlayerInspectState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(Machine.AnimData.AnimHashKey.Inspect);
        Debug.Log("[State] Inspect 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(Machine.AnimData.AnimHashKey.Inspect);
        Debug.Log("[State] Inspect 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        if (Machine.AnimData.IsActionAnimationCompleted())
            Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

using UnityEngine;

/// <summary>
/// 플레이어 Idle 상태
/// </summary>
public class PlayerIdleState : PlayerRootStateBase
{
    public PlayerIdleState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Idle);
    }

    public override void OnExit()
    {
        base.OnExit();
    }

    public override void Update(float time = 1.0f)
    {
        base.Update(time);
    }

    public override void FixedUpdate(float time = 1.0f)
    {
        base.FixedUpdate(time);
    }
}

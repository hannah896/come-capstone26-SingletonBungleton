using UnityEngine;

/// <summary>
/// 플레이어 Idle 상태
/// </summary>
public class PlayerIdleState : PlayerSubStateBase
{
    public PlayerIdleState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Idle 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Idle 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        if (Input.HasMoveInput)
        {
            var ground = GetRootState<PlayerGroundState>();
            if (ground == null) return;

            if (Input.SprintHeld)
                ground.ChangeToRun();
            else
                ground.ChangeToWalk();
        }
    }
}

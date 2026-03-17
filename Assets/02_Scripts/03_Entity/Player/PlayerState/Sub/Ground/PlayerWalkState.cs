using UnityEngine;

/// <summary>
/// 플레이어 걷기 상태
/// </summary>
public class PlayerWalkState : PlayerSubStateBase
{
    public PlayerWalkState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Walk 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Walk 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        var ground = GetRootState<PlayerGroundState>();
        if (ground == null) return;

        if (!Input.HasMoveInput)
        {
            ground.ChangeToIdle();
            return;
        }

        if (Input.SprintHeld)
        {
            ground.ChangeToRun();
        }
    }
}

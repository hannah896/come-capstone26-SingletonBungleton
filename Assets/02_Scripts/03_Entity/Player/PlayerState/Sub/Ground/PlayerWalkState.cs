using UnityEngine;

/// <summary>
/// 플레이어 걷기 상태
/// </summary>
public class PlayerWalkState : PlayerSubStateBase
{
    private const float rotationSpeed = 10f;

    public PlayerWalkState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Walk);
        Debug.Log("[State] Walk 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.StopAnimation(Machine.AnimData.AnimHashKey.Walk);
        Debug.Log("[State] Walk 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        var ground = GetRootState<PlayerLocomotionState>();
        if (ground == null) return;

        if (!Input.HasMoveInput)
        {
            ground.ChangeToIdle();
            return;
        }

        // 카메라 기준 이동
        Vector3 dir = CalcCameraRelativeDir(Input.MoveInput);
        Entity.Motor.SetHorizontalVelocity(dir, Entity.Stat.MoveSpeed);
        Entity.Motor.RotateToward(dir, rotationSpeed, time);

        if (Input.SprintHeld)
        {
            ground.ChangeToRun();
        }
    }
}

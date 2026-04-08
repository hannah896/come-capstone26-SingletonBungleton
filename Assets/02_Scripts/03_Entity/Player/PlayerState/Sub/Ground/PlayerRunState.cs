using UnityEngine;

/// <summary>
/// 플레이어 달리기 상태
/// </summary>
public class PlayerRunState : PlayerSubStateBase
{
    private const float rotationSpeed = 12f;

    public PlayerRunState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Run);
        Debug.Log("[State] Run 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.StopAnimation(Machine.AnimData.AnimHashKey.Run);
        Debug.Log("[State] Run 퇴장");
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

        // 카메라 기준 이동 (SprintMultiplier 적용)
        Vector3 dir = CalcCameraRelativeDir(Input.MoveInput);
        Entity.Motor.SetHorizontalVelocity(dir, Entity.Stat.MoveSpeed * Entity.Stat.SprintMultiplier);
        Entity.Motor.RotateToward(dir, rotationSpeed, time);

        if (!Input.SprintHeld)
        {
            ground.ChangeToWalk();
        }
    }
}

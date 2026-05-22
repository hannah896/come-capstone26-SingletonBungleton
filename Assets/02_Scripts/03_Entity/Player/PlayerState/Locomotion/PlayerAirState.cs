using UnityEngine;

/// <summary>
/// 플레이어 공중 상태 (Sub, Locomotion 하위)
/// 공중 이동 제어 + 착지 시 Idle로 복귀
/// </summary>
public class PlayerAirState : PlayerSubStateBase
{
    private const float airControlFactor = 0.5f;
    private const float airRotationSpeed = 8f;

    public PlayerAirState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Jump);
        Debug.Log("[State] Air 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.StopAnimation(Machine.AnimData.AnimHashKey.Jump);
        Debug.Log("[State] Air 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        // 공중 이동 제어 (지상 속도의 50%)
        Vector3 dir = CalcCameraRelativeDir(Input.MoveInput);
        Entity.Motor.SetHorizontalVelocity(dir, Entity.Stat.MoveSpeed * airControlFactor);

        if (dir.sqrMagnitude > 0.01f)
            Entity.Motor.RotateToward(dir, airRotationSpeed);

        // 착지 감지
        if (Entity.Motor.IsGrounded && Entity.Motor.VerticalVelocity <= 0f)
        {
            var locomotion = GetRootState<PlayerLocomotionState>();
            if (locomotion != null)
                locomotion.ChangeToIdle();
        }
    }
}

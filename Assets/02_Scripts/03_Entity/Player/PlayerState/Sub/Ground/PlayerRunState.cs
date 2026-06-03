using UnityEngine;

/// <summary>
/// 플레이어 달리기 상태
/// </summary>
public class PlayerRunState : PlayerSubStateBase
{
    private const float rotationSpeed = 12f;

    // 달리기 발소리(Walk2/Walk3 번갈아) 재생 주기
    private const float footstepInterval = 0.5f;
    private float footstepTimer;
    private bool footstepToggle;

    public PlayerRunState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Run);
        footstepTimer = footstepInterval; // 진입 즉시 첫 발소리
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
        Entity.Motor.RotateToward(dir, rotationSpeed);

        // 달리기 발소리 — Walk2/Walk3을 번갈아 재생해 뛰는 느낌
        footstepTimer += time;
        if (footstepTimer >= footstepInterval)
        {
            Extensions.PlaySFX(footstepToggle ? AudioLibrarySounds.Walk3 : AudioLibrarySounds.Walk2);
            footstepToggle = !footstepToggle;
            footstepTimer = 0f;
        }

        if (!Input.SprintHeld)
        {
            ground.ChangeToWalk();
        }
    }
}

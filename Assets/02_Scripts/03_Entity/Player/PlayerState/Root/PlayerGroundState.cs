using UnityEngine;
/// <summary>
/// 플레이어 이동 상태 (Root)
/// Sub: Idle, Walk, Run, Air
/// </summary>
public class PlayerLocomotionState : PlayerRootStateBase
{
    private PlayerIdleState idleState;
    private PlayerWalkState walkState;
    private PlayerRunState runState;
    private PlayerAirState airState;

    public PlayerLocomotionState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();

        Machine.AnimData.SetRootState(Machine.AnimData.AnimHashKey.Locomotion);

        idleState = new PlayerIdleState(Machine);
        walkState = new PlayerWalkState(Machine);
        runState = new PlayerRunState(Machine);
        airState = new PlayerAirState(Machine);

        SubStateMachine.Init(idleState);

        Debug.Log("[State] Locomotion 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Locomotion 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 점프 입력 (지면 또는 코요테 타임)
        if (Input.JumpPressed && Motor.CanJump
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            StartJump(time);
            return;
        }

        // 낙하 감지 (코요테 타임 지나면)
        if (!Motor.IsGrounded && !Motor.WasGroundedRecently
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            ChangeToAir();
            return;
        }

        // 도구 사용 (좌클릭) → 장착 도구 타입에 맞는 ActionState 전환
        if (Input.ToolUsePressed && Motor.IsGrounded
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            if (Entity.Inventory != null && Entity.Inventory.TryGetToolActionType(out ActionType actionType))
            {
                Machine.ChangeState(new PlayerActionState(Machine, actionType));
                return;
            }
        }

        // Sub 상태 업데이트
        base.Update(time);
    }

    // Sub 전환 헬퍼
    public void ChangeToIdle() => SubStateMachine.ChangeState(idleState);
    public void ChangeToWalk() => SubStateMachine.ChangeState(walkState);
    public void ChangeToRun() => SubStateMachine.ChangeState(runState);
    public void ChangeToAir() => SubStateMachine.ChangeState(airState);

    private void StartJump(float time)
    {
        if (Input.HasMoveInput)
        {
            Vector3 dir = CalcCameraRelativeDir(Input.MoveInput);
            float speed = Entity.Stat.MoveSpeed;

            if (Input.SprintHeld)
                speed *= Entity.Stat.SprintMultiplier;

            Motor.SetHorizontalVelocity(dir, speed);
            Motor.RotateToward(dir, 12f);
        }

        Motor.SetVerticalVelocity(Entity.Stat.JumpForce);
        ChangeToAir();
    }

}

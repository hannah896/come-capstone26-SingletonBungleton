using UnityEngine;

/// <summary>
/// 플레이어 지면 상태 (Root)
/// Sub: Idle, Walk, Run, Crouch
/// </summary>
public class PlayerGroundState : PlayerRootStateBase
{
    private PlayerIdleState idleState;
    private PlayerWalkState walkState;
    private PlayerRunState runState;

    public PlayerGroundState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();

        idleState = new PlayerIdleState(Machine);
        walkState = new PlayerWalkState(Machine);
        runState = new PlayerRunState(Machine);

        SubStateMachine.Init(idleState);

        Debug.Log("[State] Ground 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Ground 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 점프 입력 체크 (착지 상태에서만)
        if (Input.JumpPressed && Entity.IsGrounded)
        {
            Machine.ChangeState(new PlayerAirState(Machine));
            return;
        }

        // 공격 입력 체크
        if (Input.AttackPressed)
        {
            Machine.ChangeState(new PlayerCombatState(Machine));
            return;
        }

        // Sub 상태 업데이트
        base.Update(time);
    }

    // Sub 전환 헬퍼
    public void ChangeToIdle() => SubStateMachine.ChangeState(idleState);
    public void ChangeToWalk() => SubStateMachine.ChangeState(walkState);
    public void ChangeToRun() => SubStateMachine.ChangeState(runState);
}

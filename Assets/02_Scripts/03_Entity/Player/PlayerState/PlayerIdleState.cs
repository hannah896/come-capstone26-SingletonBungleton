using UnityEngine;

/// <summary>
/// 플레이어 Idle 상태
/// </summary>
public class PlayerIdleState : PlayerRootStateBase
{
    public PlayerIdleState(StateMachine<StateBase> stateMachine) : base(stateMachine)
    {
    }

    public override void OnEnter()
    {
        //Idle 애니메이션 재생
        if (machine != null && machine.AnimData != null)
        {
            machine.AnimData.PlayLocomotionAnimation(machine.AnimData.AnimHashKey.Idle);
        }
    }

    public override void OnExit()
    {
        // Idle 종료 시 처리
    }

    public override void Update()
    {
        // TODO: 입력에 따라 다른 상태로 전환
    }

    public override void FixedUpdate()
    {
        // 물리 업데이트
    }
}

using UnityEngine;

/// <summary>
/// 플레이어 사망 상태 (Root)
/// </summary>
public class PlayerDeadState : PlayerRootStateBase
{
    public PlayerDeadState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Dead 진입");
        // TODO: 사망 애니메이션 재생, 입력 비활성화
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Dead 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 사망 상태에서는 아무것도 하지 않음
        // 부활 시 외부에서 Locomotion으로 전환
    }
}

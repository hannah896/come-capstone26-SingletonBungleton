using UnityEngine;

/// <summary>
/// 플레이어 수면 상태 (Root)
/// </summary>
public class PlayerSleepState : PlayerRootStateBase
{
    public PlayerSleepState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Sleep 진입");
        // TODO: 수면 애니메이션 재생, 이동 비활성화
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Sleep 퇴장");
    }

    public override void Update(float time = 1)
    {
        // TODO: 수면 완료 조건 또는 중단 입력 시 Locomotion으로 복귀
    }
}

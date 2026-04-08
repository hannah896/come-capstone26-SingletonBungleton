using UnityEngine;

/// <summary>
/// 플레이어 행동 상태 (Root)
/// Sub: Pick, Mine, Chop, Dig, Ignite, Cook, Inspect, Build
/// </summary>
public class PlayerActionState : PlayerRootStateBase
{
    public PlayerActionState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Action 진입");
        // TODO: 상호작용 타입에 따라 적절한 Sub 상태로 초기화
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Action 퇴장");
    }

    public override void Update(float time = 1)
    {
        // TODO: 행동 완료 시 Locomotion으로 복귀
        base.Update(time);
    }
}

using UnityEngine;

/// <summary>
/// 플레이어 전투 상태 (Root) — 스켈레톤
/// Sub: Attack (추후 구현)
/// </summary>
public class PlayerCombatState : PlayerRootStateBase
{
    public PlayerCombatState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Combat 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Combat 퇴장");
    }

    public override void Update(float time = 1)
    {
        // TODO: 공격 완료 시 Ground로 전환
        // TODO: Sub 상태 (Attack) 구현 후 base.Update() 호출

        // 임시: 즉시 Ground로 복귀 (공격 로직 구현 전)
    }
}

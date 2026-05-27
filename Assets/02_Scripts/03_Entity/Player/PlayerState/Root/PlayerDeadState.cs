using UnityEngine;

/// <summary>
/// 플레이어 사망 상태 (Root)
/// wasHit=true  → Hit 트리거 + Dead bool → CombatDeath01 (피격 사망)
/// wasHit=false → Dead bool만          → CombatDeath02 (자연사)
/// </summary>
public class PlayerDeadState : PlayerRootStateBase
{
    private readonly bool wasHit;

    public PlayerDeadState(PlayerRootStateMachine machine, bool wasHit = false) : base(machine)
    {
        this.wasHit = wasHit;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayDeadAnimation(wasHit);
        Debug.Log($"[State] Dead 진입 (wasHit={wasHit})");
        // TODO: 입력 비활성화, 리스폰 로직
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

using UnityEngine;

/// <summary>
/// 플레이어 피격 상태 (Root)
/// </summary>
public class PlayerHurtState : PlayerRootStateBase
{
    public PlayerHurtState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Hurt 진입");
        // TODO: 피격 애니메이션 재생
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Hurt 퇴장");
    }

    public override void Update(float time = 1)
    {
        // TODO: 피격 애니메이션 완료 시 Locomotion으로 복귀
        // 체력 0 이하면 Dead로 전환
        if (Entity.Stat.IsDead)
        {
            Machine.ChangeState(new PlayerDeadState(Machine));
            return;
        }

        Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

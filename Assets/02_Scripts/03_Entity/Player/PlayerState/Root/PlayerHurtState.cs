using UnityEngine;

/// <summary>
/// 플레이어 피격 상태 (Root)
/// </summary>
public class PlayerHurtState : PlayerRootStateBase
{
    // 피격 애니메이션 최소 재생 시간 (애니메이터 연동 전 안전망)
    private const float hurtDuration = 0.7f;
    private float elapsedTime;

    public PlayerHurtState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        elapsedTime = 0f;
        Machine.AnimData.SetRootState(Machine.AnimData.AnimHashKey.Hurt);
        Debug.Log("[State] Hurt 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Hurt 퇴장");
    }

    public override void Update(float time = 1)
    {
        elapsedTime += time;
        if (elapsedTime < hurtDuration) return;

        if (Entity.Stat.IsDead)
        {
            Machine.ChangeState(new PlayerDeadState(Machine, wasHit: true));
            return;
        }

        Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

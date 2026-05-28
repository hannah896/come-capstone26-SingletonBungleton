using UnityEngine;

/// <summary>
/// 플레이어 공격 상태 (Root)
/// </summary>
public class PlayerAttackState : PlayerRootStateBase
{
    public PlayerAttackState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.SetRootState(Machine.AnimData.AnimHashKey.Attack);
        Debug.Log("[State] Attack 진입");
        // TODO: 공격 애니메이션 재생
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Attack 퇴장");
    }

    public override void Update(float time = 1)
    {
        // TODO: 공격 애니메이션 완료 시 Locomotion으로 복귀
        Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

using UnityEngine;

/// <summary>
/// 점화 상태 (Sub, Action 하위)
/// </summary>
public class PlayerIgniteState : PlayerSubStateBase
{
    public PlayerIgniteState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Ignite 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Ignite 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 점화 애니메이션 완료 시 Action 종료
    }
}

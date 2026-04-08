using UnityEngine;

/// <summary>
/// 뽑기 상태 (Sub, Action 하위)
/// </summary>
public class PlayerDigState : PlayerSubStateBase
{
    public PlayerDigState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Dig 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Dig 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 뽑기 애니메이션 완료 시 Action 종료
    }
}

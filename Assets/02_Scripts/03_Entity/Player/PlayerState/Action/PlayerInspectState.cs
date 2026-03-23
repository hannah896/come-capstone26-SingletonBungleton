using UnityEngine;

/// <summary>
/// 조사 상태 (Sub, Action 하위)
/// </summary>
public class PlayerInspectState : PlayerSubStateBase
{
    public PlayerInspectState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Inspect 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Inspect 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 조사 애니메이션 완료 시 Action 종료
    }
}

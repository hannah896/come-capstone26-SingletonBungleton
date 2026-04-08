using UnityEngine;

/// <summary>
/// 벌목 상태 (Sub, Action 하위)
/// </summary>
public class PlayerChopState : PlayerSubStateBase
{
    public PlayerChopState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Chop 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Chop 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 벌목 애니메이션 완료 시 Action 종료
    }
}

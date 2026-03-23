using UnityEngine;

/// <summary>
/// 요리 상태 (Sub, Action 하위)
/// </summary>
public class PlayerCookState : PlayerSubStateBase
{
    public PlayerCookState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Cook 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Cook 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 요리 애니메이션 완료 시 Action 종료
    }
}

using UnityEngine;

/// <summary>
/// 채집 상태 (Sub, Action 하위)
/// </summary>
public class PlayerPickState : PlayerSubStateBase
{
    public PlayerPickState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Pick 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Pick 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 채집 애니메이션 완료 시 Action 종료
    }
}

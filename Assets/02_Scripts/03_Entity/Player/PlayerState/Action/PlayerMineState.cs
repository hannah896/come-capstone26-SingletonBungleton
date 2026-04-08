using UnityEngine;

/// <summary>
/// 채굴 상태 (Sub, Action 하위)
/// </summary>
public class PlayerMineState : PlayerSubStateBase
{
    public PlayerMineState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Mine 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Mine 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 채굴 애니메이션 완료 시 Action 종료
    }
}

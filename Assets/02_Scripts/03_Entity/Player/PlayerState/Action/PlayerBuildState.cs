using UnityEngine;

/// <summary>
/// 설치 상태 (Sub, Action 하위)
/// </summary>
public class PlayerBuildState : PlayerSubStateBase
{
    public PlayerBuildState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Build 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Build 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
        // TODO: 설치 완료 시 Action 종료
    }
}

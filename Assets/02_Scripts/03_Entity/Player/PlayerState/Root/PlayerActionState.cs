using UnityEngine;

/// <summary>
/// 플레이어 행동 상태 (Root)
/// Sub: Pick, Mine, Chop, Dig, Ignite, Cook, Inspect, Build
/// </summary>
public class PlayerActionState : PlayerRootStateBase
{
    private readonly ActionType actionType;

    public PlayerActionState(PlayerRootStateMachine machine, ActionType actionType) : base(machine)
    {
        this.actionType = actionType;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log($"[State] Action 진입 - {actionType}");

        PlayerSubStateBase subState = actionType switch
        {
            ActionType.Pick    => new PlayerPickState(Machine),
            ActionType.Mine    => new PlayerMineState(Machine),
            ActionType.Chop    => new PlayerChopState(Machine),
            ActionType.Dig     => new PlayerDigState(Machine),
            ActionType.Ignite  => new PlayerIgniteState(Machine),
            ActionType.Cook    => new PlayerCookState(Machine),
            ActionType.Inspect => new PlayerInspectState(Machine),
            ActionType.Build   => new PlayerBuildState(Machine),
            _                  => new PlayerPickState(Machine),
        };

        SubStateMachine.Init(subState);
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Action 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
    }
}

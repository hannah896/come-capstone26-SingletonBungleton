using UnityEngine;

/// <summary>
/// 플레이어 도구/상호작용 액션 상태입니다.
/// Sub: Pick, Mine, Chop, Dig, Ignite, Cook, Inspect, Build
/// </summary>
public class PlayerActionState : PlayerRootStateBase
{
    // 애니메이터가 액션을 끝내지 못하고 멈춘 경우를 대비한 안전망 (정상 흐름은 애니메이터 복귀로 종료)
    private const float maxActionDuration = 5f;

    private readonly ActionType actionType;
    private float elapsedTime;

    public PlayerActionState(PlayerRootStateMachine machine, ActionType actionType) : base(machine)
    {
        this.actionType = actionType;
    }

    public override void OnEnter()
    {
        base.OnEnter();
        elapsedTime = 0f;
        Machine.AnimData.SetRootState(Machine.AnimData.AnimHashKey.Action);
        Debug.Log($"[State] Action 진입 - {actionType}");

        PlayerSubStateBase subState = actionType switch
        {
            ActionType.Pick => new PlayerPickState(Machine),
            ActionType.Mine => new PlayerMineState(Machine),
            ActionType.Chop => new PlayerChopState(Machine),
            ActionType.Dig => new PlayerDigState(Machine),
            ActionType.Ignite => new PlayerIgniteState(Machine),
            ActionType.Cook => new PlayerCookState(Machine),
            ActionType.Inspect => new PlayerInspectState(Machine),
            ActionType.Build => new PlayerBuildState(Machine),
            _ => new PlayerPickState(Machine),
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
        elapsedTime += time;
        base.Update(time);

        // 하위 상태가 이미 Locomotion으로 전환했다면 중복 전환 방지
        if (Machine.CurrentState != this) return;

        if (elapsedTime >= maxActionDuration)
        {
            Debug.LogWarning($"[State] Action timeout - {actionType}. Locomotion으로 복귀합니다.");
            ChangeToLocomotion();
        }
    }

    public void ChangeToLocomotion()
    {
        Machine.ChangeState(new PlayerLocomotionState(Machine));
    }
}

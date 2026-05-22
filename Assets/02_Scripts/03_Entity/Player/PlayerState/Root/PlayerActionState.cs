using UnityEngine;

/// <summary>
/// 플레이어 도구/상호작용 액션 상태입니다.
/// Sub: Pick, Mine, Chop, Dig, Ignite, Cook, Inspect, Build
/// </summary>
public class PlayerActionState : PlayerRootStateBase
{
    private const float minActionDuration = 0.15f;
    private const float maxActionDuration = 2.5f;

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

        if (TryInterruptAction())
            return;

        base.Update(time);

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

    // 모든 인터럽트 입력은 PlayerInputData(New Input System 경유)에서만 읽는다.
    private bool TryInterruptAction()
    {
        if (elapsedTime < minActionDuration) return false;

        if (Input.AttackPressed)
        {
            Debug.Log($"[State] Action interrupt by Attack - {actionType}");
            Machine.ChangeState(new PlayerAttackState(Machine));
            return true;
        }

        if (Input.JumpPressed && Motor.CanJump)
        {
            Debug.Log($"[State] Action interrupt by Jump - {actionType}");
            Motor.SetVerticalVelocity(Entity.Stat.JumpForce);
            ChangeToLocomotion();
            return true;
        }

        if (Input.HasMoveInput || Input.TracePressed)
        {
            Debug.Log($"[State] Action interrupt by Move/Trace - {actionType}");
            ChangeToLocomotion();
            return true;
        }

        return false;
    }
}

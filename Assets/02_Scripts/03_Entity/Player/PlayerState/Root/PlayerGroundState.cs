using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 플레이어 이동 상태 (Root)
/// Sub: Idle, Walk, Run, Trace, Air
/// </summary>
public class PlayerLocomotionState : PlayerRootStateBase
{
    private PlayerIdleState idleState;
    private PlayerWalkState walkState;
    private PlayerRunState runState;
    private PlayerTraceState traceState;
    private PlayerAirState airState;

    public PlayerLocomotionState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();

        idleState = new PlayerIdleState(Machine);
        walkState = new PlayerWalkState(Machine);
        runState = new PlayerRunState(Machine);
        traceState = new PlayerTraceState(Machine);
        airState = new PlayerAirState(Machine);

        SubStateMachine.Init(idleState);

        Debug.Log("[State] Locomotion 진입");
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Locomotion 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 자원 클릭 → Trace (공중 제외)
        if (Input.TracePressed && Motor.IsGrounded
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            ChangeToTrace();
            return;
        }

        // 점프 입력 (지면 또는 코요테 타임)
        if (Input.JumpPressed && Motor.WasGroundedRecently
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            Motor.SetVerticalVelocity(Entity.Stat.JumpForce);
            ChangeToAir();
            return;
        }

        // 낙하 감지 (코요테 타임 지나면)
        if (!Motor.IsGrounded && !Motor.WasGroundedRecently
            && !(SubStateMachine.CurrentState is PlayerAirState))
        {
            ChangeToAir();
            return;
        }

        // 공격 입력
        if (Input.AttackPressed)
        {
            Machine.ChangeState(new PlayerAttackState(Machine));
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ── 테스트 입력: 숫자키 1~8로 Action 직접 트리거 ─────────────
        // 1=Pick  2=Mine  3=Chop  4=Dig  5=Ignite  6=Cook  7=Inspect  8=Build
        if (Motor.IsGrounded) CheckTestActionKeys();
#endif

        // Sub 상태 업데이트
        base.Update(time);
    }

    // Sub 전환 헬퍼
    public void ChangeToIdle() => SubStateMachine.ChangeState(idleState);
    public void ChangeToWalk() => SubStateMachine.ChangeState(walkState);
    public void ChangeToRun() => SubStateMachine.ChangeState(runState);
    public void ChangeToTrace() => SubStateMachine.ChangeState(traceState);
    public void ChangeToAir() => SubStateMachine.ChangeState(airState);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 숫자키 1~8로 Action 상태를 즉시 진입시킨다 (테스트 전용).
    /// uloop simulate-keyboard --key digit1 ~ digit8 로 원격 트리거 가능.
    /// </summary>
    private void CheckTestActionKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        ActionType? action = null;
        if      (kb.digit1Key.wasPressedThisFrame) action = ActionType.Pick;
        else if (kb.digit2Key.wasPressedThisFrame) action = ActionType.Mine;
        else if (kb.digit3Key.wasPressedThisFrame) action = ActionType.Chop;
        else if (kb.digit4Key.wasPressedThisFrame) action = ActionType.Dig;
        else if (kb.digit5Key.wasPressedThisFrame) action = ActionType.Ignite;
        else if (kb.digit6Key.wasPressedThisFrame) action = ActionType.Cook;
        else if (kb.digit7Key.wasPressedThisFrame) action = ActionType.Inspect;
        else if (kb.digit8Key.wasPressedThisFrame) action = ActionType.Build;

        if (action.HasValue)
        {
            Debug.Log($"[TEST] Action 트리거: {action.Value} (키: {(int)action.Value})");
            Machine.ChangeState(new PlayerActionState(Machine, action.Value));
        }
    }
#endif
}

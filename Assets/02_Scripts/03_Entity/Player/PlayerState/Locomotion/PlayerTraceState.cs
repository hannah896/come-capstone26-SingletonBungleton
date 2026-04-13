using UnityEngine;

/// <summary>
/// 플레이어 추적 상태 (Sub, Locomotion 하위)
/// 클릭한 자원 오브젝트 표면 stopDistance(1f) 지점까지 Walk 속도로 자동 이동합니다.
/// WASD 입력 또는 점프 시 즉시 수동 취소됩니다.
/// </summary>
public class PlayerTraceState : PlayerSubStateBase
{
    private const float rotationSpeed = 10f;
    private const float arrivalThreshold = 0.25f;

    private Vector3 destination;

    public PlayerTraceState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        destination = Input.TraceDestination;
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Trace);
        Debug.Log($"[State] Trace 진입 → 목적지 {destination}");
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.StopAnimation(Machine.AnimData.AnimHashKey.Trace);
        Debug.Log("[State] Trace 퇴장");
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        var locomotion = GetRootState<PlayerLocomotionState>();
        if (locomotion == null) return;

        // WASD 또는 점프 입력 시 수동 취소
        if (Input.HasMoveInput || Input.JumpPressed)
        {
            locomotion.ChangeToIdle();
            return;
        }

        // 목적지까지 수평 거리 계산
        Vector3 toTarget = destination - Entity.transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < arrivalThreshold)
        {
            Entity.Motor.SetHorizontalVelocity(Vector3.zero, 0f);
            locomotion.ChangeToIdle();
            return;
        }

        // Walk 속도로 목적지 방향 이동
        Vector3 dir = toTarget.normalized;
        Entity.Motor.SetHorizontalVelocity(dir, Entity.Stat.MoveSpeed);
        Entity.Motor.RotateToward(dir, rotationSpeed, time);
    }
}

using UnityEngine;

/// <summary>
/// 플레이어 추적 상태 (Sub, Locomotion 하위)
/// Walk 속도로 대상을 따라감
/// </summary>
public class PlayerTraceState : PlayerSubStateBase
{
    private const float rotationSpeed = 10f;

    public PlayerTraceState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayLocomotionAnimation(Machine.AnimData.AnimHashKey.Trace);
        Debug.Log("[State] Trace 진입");
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

        // TODO: 추적 대상 방향으로 이동
        // Vector3 dir = (target.position - Entity.transform.position).normalized;
        // Entity.Motor.SetHorizontalVelocity(dir, Entity.Stat.MoveSpeed);
        // Entity.Motor.RotateToward(dir, rotationSpeed, time);

        // TODO: 추적 대상 도달 또는 범위 이탈 시 전환
    }
}

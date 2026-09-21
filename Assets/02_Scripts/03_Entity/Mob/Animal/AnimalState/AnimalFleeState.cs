using UnityEngine;

/// <summary>
/// 도망 상태. 때린 플레이어의 반대 방향(맞은 방향의 반대)으로 뛴다.
/// 최소 MinFleeDuration 동안은 무조건 도망치고, 그 뒤 공격자와 FleeRange 이상 벌어지면 Idle로 복귀한다.
/// 공격자가 쫓아오면 그 현재 위치 기준으로 방향을 다시 잡는다.
/// </summary>
public class AnimalFleeState : MobState<Animal>
{
    private readonly AnimalStateMachine sm;
    private float repathTimer;
    private float elapsed;

    public AnimalFleeState(Animal owner, AnimalStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.PlayAnim(AnimalAnimId.Run);
        repathTimer = 0f; // 진입 즉시 첫 목적지 계산
        elapsed = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;
        Vector3 threatPosition = Owner.GetAttackerPosition();

        // 최소 도망 시간이 지났고 공격자와 충분히 멀어졌으면 도망 종료
        Vector3 offset = Owner.transform.position - threatPosition;
        offset.y = 0f;
        if (elapsed >= Owner.MinFleeDuration && offset.sqrMagnitude >= Owner.FleeRange * Owner.FleeRange)
        {
            sm.ToIdle();
            return;
        }

        repathTimer -= time;
        if (repathTimer > 0f && !Owner.HasArrived()) return;

        repathTimer = Owner.FleeRepathInterval;
        if (Owner.TryGetFleePoint(threatPosition, out var point))
            Owner.MoveTo(point, Owner.RunSpeed);
    }

    public override void OnExit()
        => Owner.StopMoving();
}

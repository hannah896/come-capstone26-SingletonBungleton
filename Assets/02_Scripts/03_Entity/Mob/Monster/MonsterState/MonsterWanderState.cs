using UnityEngine;

/// <summary>
/// 배회 상태. 홈(첫 대기 위치) 주변 WanderRadius 안의 임의 지점으로 걸어간 뒤 Idle로 돌아간다.
/// 이동 중에도 매 프레임 플레이어를 탐색해 발견 시 Chase로 전환한다.
/// </summary>
public class MonsterWanderState : MobState<Monster>
{
    // 목표에 이만큼 가까워지면 도착으로 본다
    private const float ArriveDistance = 0.3f;

    private readonly MonsterStateMachine sm;
    private Vector3 destination;
    private float remaining; // 지형 등에 막혀 도착하지 못할 때를 대비한 최대 이동 시간

    public MonsterWanderState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        destination = Owner.PickWanderPoint();
        float speed = Owner.WanderSpeed;
        float distance = Vector3.Distance(Owner.transform.position, destination);
        remaining = speed > 0f ? distance / speed + 1f : 0f;
        Owner.PlayAnim(MonsterAnimId.Move);
    }

    public override void Update(float time = 1.0f)
    {
        if (Owner.AcquireTarget())
        {
            sm.ToChase();
            return;
        }

        remaining -= time;
        if (remaining <= 0f || Owner.WanderStep(destination, time, ArriveDistance))
            sm.ToIdle();
    }
}

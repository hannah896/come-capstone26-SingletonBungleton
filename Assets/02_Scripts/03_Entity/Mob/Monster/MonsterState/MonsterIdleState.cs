using UnityEngine;

/// <summary>
/// 대기 상태. 매 프레임 DetectRange/FOV로 플레이어를 탐색하고, 발견 시 Chase로 전환한다.
/// 일정 시간(WanderIdleTime 전후) 아무도 못 찾으면 Wander로 넘어가 주변을 돌아다닌다.
/// </summary>
public class MonsterIdleState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;
    private float wanderTimer;

    public MonsterIdleState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        Owner.PlayAnim(MonsterAnimId.Idle);
        Owner.EnsureWanderHome();

        // 여러 마리가 동시에 움직이지 않도록 ±30% 흔든다
        float baseTime = Owner.WanderIdleTime;
        wanderTimer = baseTime * Random.Range(0.7f, 1.3f);
    }

    public override void Update(float time = 1.0f)
    {
        if (Owner.AcquireTarget())
        {
            sm.ToChase();
            return;
        }

        if (!Owner.CanWander) return;

        wanderTimer -= time;
        if (wanderTimer <= 0f)
            sm.ToWander();
    }
}

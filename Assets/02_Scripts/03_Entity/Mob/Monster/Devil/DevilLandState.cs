using UnityEngine;

/// <summary>
/// 착지 상태. 비행 지속 시간이 끝났거나 타깃을 잃었을 때 지상으로 내려앉는다.
/// 비행 중 이동해 지형 높이가 달라졌을 수 있으므로 착지 지점은 레이캐스트로 다시 구한다.
/// </summary>
public class DevilLandState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;

    private float startY;
    private float targetY;
    private float progress;

    public DevilLandState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    // 착지 도중 피격돼도 Hit 상태로 끊지 않는다(공중에 멈춘 채 남는 것 방지).
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        startY = devil.transform.position.y;
        targetY = devil.FindLandingY();
        progress = 0f;

        // 자세는 착지 시작 시점에 지상으로 되돌린다. 재이륙 쿨타임도 여기서 걸린다.
        devil.EnterGroundStance();
        devil.PlayAnim(MonsterAnimId.Idle);
    }

    public override void Update(float time = 1.0f)
    {
        float duration = Mathf.Max(devil.LandDuration, 0.0001f);
        progress += time / duration;
        float p = Mathf.Clamp01(progress);

        Vector3 pos = devil.transform.position;
        devil.transform.position = new Vector3(pos.x, Mathf.Lerp(startY, targetY, p), pos.z);

        if (progress < 1f) return;

        if (!devil.IsTargetValid())
        {
            devil.ClearTarget();
            sm.ToIdle();
            return;
        }

        sm.ToAttack();
    }
}

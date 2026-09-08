using UnityEngine;

/// <summary>
/// 급강하 할퀴기(Fly Slash Attack). 공중에서 타깃 발밑으로 내리꽂고 착지 지점에 광역 피해를 준다.
/// DemonLeapState의 포물선 이동을 "아래로 꽂는" 궤적으로 바꿔 재사용한 형태다.
///
/// 급강하 직후에는 지상 자세로 내려앉는다(기획: 비행 → 지상 전환 조건 ③ 일시 착지).
/// 재이륙 쿨타임은 착지 처리(DevilLandState → EnterGroundStance)에서 걸린다.
/// </summary>
public class DevilDiveState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;

    private Vector3 startPos;
    private Vector3 landPos;
    private float progress;

    public DevilDiveState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    // 급강하 도중 피격돼도 궤적이 끊기지 않게 한다.
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        startPos = devil.transform.position;

        // 시전 시점의 타깃 발밑을 착지 지점으로 고정한다(이후 플레이어가 피하면 빗나간다).
        Vector3 aim = devil.Target != null ? devil.Target.transform.position : devil.transform.position;
        landPos = new Vector3(aim.x, aim.y, aim.z);

        Vector3 dir = landPos - startPos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            devil.transform.rotation = Quaternion.LookRotation(dir.normalized);

        devil.PlayAnim(MonsterAnimId.FlyAttack);
        devil.StartDiveCooldown();
        progress = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        float duration = Mathf.Max(devil.DiveDuration, 0.0001f);
        progress += time / duration;
        float p = Mathf.Clamp01(progress);

        // 수평은 선형, 수직은 뒤로 갈수록 빨라지게(p²) — "내리꽂는" 느낌을 준다.
        Vector3 flat = Vector3.Lerp(startPos, landPos, p);
        float y = Mathf.Lerp(startPos.y, landPos.y, p * p);
        devil.transform.position = new Vector3(flat.x, y, flat.z);

        if (progress < 1f) return;

        // 착지 지점 광역 피해
        devil.PerformDiveImpact(landPos);

        // 급강하 직후는 일시 착지 — 지상 자세로 되돌린다.
        sm.ToLand();
    }
}

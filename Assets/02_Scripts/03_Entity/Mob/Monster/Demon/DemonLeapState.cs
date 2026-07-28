using UnityEngine;

/// <summary>
/// 데몬 도약 상태. 루트 모션 없이 스크립트로 루트 Transform을 포물선으로 이동시킨다.
/// 콜라이더가 루트에 붙어 있으므로 도약 동안 콜라이더도 함께 떠오른다.
/// 착지하면 다시 공격 판단(ToAttack)으로 복귀하고, 타깃이 사라졌으면 Idle로 간다.
/// </summary>
public class DemonLeapState : MobState<Monster>
{
    private readonly DemonStateMachine sm;
    private readonly Demon demon;

    private Vector3 startPos;
    private Vector3 landPos;
    private float progress; // 0→1 정규화 진행도

    public DemonLeapState(Demon owner, DemonStateMachine machine) : base(owner, machine)
    {
        demon = owner;
        sm = machine;
    }

    // 공중에 떠 있는 동안 피격돼도 Hit 상태로 끊기지 않는다.
    // (포물선이 중간에 끊기면 루트 Y가 공중에 멈춘 채 남을 수 있으므로 도약은 끝까지 유지한다.)
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        startPos = demon.transform.position;

        Vector3 dir = demon.LeapDirection();
        landPos = startPos + dir * demon.JumpDistance;
        landPos.y = startPos.y; // 수평 이동만; Y는 포물선으로 별도 계산

        // 도약 방향을 즉시 바라보게 한다.
        if (dir.sqrMagnitude > 0.0001f)
            demon.transform.rotation = Quaternion.LookRotation(dir);

        demon.PlayAnim(MonsterAnimId.Jump); // 매핑된 Bool이 비어 있으면 애니는 생략된다
        demon.StartLeapCooldown();
        progress = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        float duration = Mathf.Max(demon.JumpDuration, 0.0001f);
        progress += time / duration;
        float p = Mathf.Clamp01(progress);

        // 수평은 시작→착지 선형 보간, 수직은 4·p·(1-p) 포물선(정점 p=0.5에서 JumpHeight)
        Vector3 flat = Vector3.Lerp(startPos, landPos, p);
        float y = startPos.y + demon.JumpHeight * 4f * p * (1f - p);
        demon.transform.position = new Vector3(flat.x, y, flat.z);

        if (progress < 1f) return;

        // 착지 후 재판단
        if (!demon.IsTargetValid())
        {
            demon.ClearTarget();
            sm.ToIdle();
            return;
        }

        sm.ToAttack();
    }
}

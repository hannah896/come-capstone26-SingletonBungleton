using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼음 돌진. 지상 교전 상태가 고른 타깃(가장 먼 플레이어)을 향해 직선으로 달린다.
///
/// ① 예고 (ChargeWarningTime): 타깃 쪽으로 몸을 돌린 뒤 방향을 고정하고, 바닥에 경로 예고선을 깐다.
/// ② 돌진: Run 클립으로 곧게 달리며 몸에 닿는 플레이어를 한 번씩 치고 옆으로 날린다.
///    목표 거리를 다 달리거나 아레나 끝에 닿으면 멈춘다.
/// ③ 기둥(DragonCover)에 부딪히면 그 자리에서 멈추고 스스로 그로기에 빠진다.
/// </summary>
public class DragonChargeState : MobState<Monster>
{
    // 예고 시작 후 이 시간까지만 타깃 쪽으로 돈다. 이후 방향을 고정하고 예고선을 깐다.
    private const float AimTime = 0.35f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private readonly HashSet<Player> hitOnce = new HashSet<Player>();

    private float elapsed;
    private bool locked;
    private bool dashing;
    private float distance;
    private float traveled;
    private float nextTrailAt;   // 다음 흔적 장판을 남길 이동 거리 (2페이즈)

    public DragonChargeState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        locked = false;
        dashing = false;
        traveled = 0f;
        nextTrailAt = 0f;
        hitOnce.Clear();
        boss.StartCooldown(DragonSkill.Charge);
        boss.PlayAnim(MonsterAnimId.Idle);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null)
        {
            sm.ToAttack();
            return;
        }

        elapsed += time;

        if (!locked)
        {
            if (boss.Target != null)
                boss.TurnTowards(boss.Target.transform.position - boss.transform.position, time, d.AimTurnSpeed);

            if (elapsed >= AimTime) LockPath(d);
            return;
        }

        if (!dashing)
        {
            if (elapsed < d.ChargeWarningTime) return;
            dashing = true;
            boss.PlayAnim(MonsterAnimId.Run);
        }

        // 기둥에 부딪히면 스스로 그로기
        if (boss.FindCoverAhead() != null)
        {
            sm.ToGroggy(d.ChargeStunDuration, d.GroggyDamageMultiplier);
            return;
        }

        float step = Mathf.Min(d.ChargeSpeed * time, distance - traveled);
        boss.MovePlanar(boss.transform.forward * step);
        traveled += step;
        boss.ChargeHit(hitOnce);

        // 망령 돌진: 지나간 자리에 산성 웅덩이 (몸 뒤쪽, 꼬리 근처에 남긴다)
        if (d.ChargeTrailSpacing > 0f && traveled >= nextTrailAt)
        {
            nextTrailAt = traveled + d.ChargeTrailSpacing;
            boss.DropTrailPatch(boss.transform.position - boss.transform.forward * 3f);
        }

        if (traveled >= distance - 0.01f)
        {
            boss.PlayAnim(MonsterAnimId.Idle);
            sm.ToAttack();
        }
    }

    // 방향을 고정하고 달릴 거리를 정한다: 타깃 너머 조금 더, 최대 거리와 아레나 끝 중 가까운 쪽까지.
    private void LockPath(DragonStatData d)
    {
        locked = true;

        float toTarget = boss.Target != null
            ? Vector3.ProjectOnPlane(boss.Target.transform.position - boss.transform.position, Vector3.up).magnitude
            : d.ChargeMaxDistance;
        distance = Mathf.Min(toTarget + d.ChargeOvershoot, d.ChargeMaxDistance);
        distance = Mathf.Min(distance, DistanceToArenaEdge(d));
        distance = Mathf.Max(distance, 0f);

        Vector3 start = boss.transform.position;
        Vector3 end = start + boss.transform.forward * distance;
        boss.SpawnMarkerLine(start, end, d.ChargeWidth, Mathf.Max(d.ChargeWarningTime - elapsed, 0.1f));
    }

    // 정면으로 달렸을 때 아레나 원 경계까지 남은 거리 (몸 길이 여유 3m)
    private float DistanceToArenaEdge(DragonStatData d)
    {
        Vector3 c = boss.ArenaCenter;
        Vector3 p = boss.transform.position - c;
        p.y = 0f;
        Vector3 f = boss.transform.forward;
        f.y = 0f;
        f.Normalize();

        float r = Mathf.Max(d.ArenaRadius - 3f, 0f);
        // |p + f t| = r 의 양수 해
        float b = Vector3.Dot(p, f);
        float cc = p.sqrMagnitude - r * r;
        float disc = b * b - cc;
        if (disc < 0f) return 0f;
        return Mathf.Max(-b + Mathf.Sqrt(disc), 0f);
    }
}

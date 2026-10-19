using UnityEngine;

/// <summary>
/// 드래곤 지상 교전 상태. 거리·방향·쿨타임으로 다음 패턴을 고르고, 고를 게 없으면 다가간다.
///
/// 선택 순서 (조건을 만족하고 쿨이 돌아온 첫 패턴):
/// ① 이륙 대기 중         → 이륙 (공중 페이즈)
///    응시 체력            → 월식의 응시 (2페이즈)
///    최종 발악 체력       → 최종 발악 (2페이즈)
/// ② 정면 가까이           → 서리 물기 ★
/// ③ 브레스 사거리 안      → 서리 브레스
/// ④ 멀리 떨어진 플레이어 → 얼음 돌진 (가장 먼 플레이어를 노린다)
/// ⑤ 거리를 벌림           → 빙탄 3연사
/// ⑥ 전부 쿨               → 타깃 쪽으로 걷거나, 가까우면 제자리에서 몸만 돌린다
/// 패턴 사이에는 최소 간격(SkillGap)을 둔다.
/// </summary>
public class DragonGroundState : MobState<Monster>
{
    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private float gapTimer;

    public DragonGroundState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        gapTimer = boss.SkillGap;
    }

    public override void Update(float time = 1.0f)
    {
        if (!boss.IsTargetValid() && !boss.AcquireTarget())
        {
            boss.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 공중 페이즈는 하던 패턴이 끝난 뒤, 다음 판단의 맨 처음에 한다.
        if (boss.IsTakeOffPending)
        {
            sm.ToTakeOff();
            return;
        }

        if (boss.IsGazePending)
        {
            sm.ToGaze();
            return;
        }

        if (boss.IsFrenzyPending)
        {
            sm.ToFrenzy();
            return;
        }

        if (gapTimer > 0f)
        {
            gapTimer -= time;
            Approach(time);
            return;
        }

        if (TrySelect(out DragonSkill skill))
        {
            sm.ToSkill(skill);
            return;
        }

        Approach(time);
    }

    private bool TrySelect(out DragonSkill skill)
    {
        skill = default;
        var d = boss.Data;
        if (d == null) return false;

        float dist = boss.TargetDistance;
        bool facing = AngleToTarget() <= 45f;

        // ② 서리 물기: 정면에 붙은 플레이어
        if (boss.IsSkillReady(DragonSkill.Bite) && dist <= d.BiteRange && facing)
            return Pick(DragonSkill.Bite, out skill);

        // ③ 서리 브레스: 중거리 (엄폐를 강요한다)
        if (boss.IsSkillReady(DragonSkill.Breath) && dist <= d.BreathRange * 0.85f)
            return Pick(DragonSkill.Breath, out skill);

        // ④ 얼음 돌진: 멀리 떨어진 플레이어를 노린다
        if (boss.IsSkillReady(DragonSkill.Charge))
        {
            var players = boss.CollectPlayers();
            if (players.Count > 0)
            {
                Player farthest = players[players.Count - 1];
                Vector3 to = farthest.transform.position - boss.transform.position;
                to.y = 0f;
                if (to.magnitude >= d.ChargeMinRange)
                {
                    boss.SetTarget(farthest);
                    return Pick(DragonSkill.Charge, out skill);
                }
            }
        }

        // ⑤ 빙탄 3연사: 거리를 벌린 플레이어를 노린다
        if (boss.IsSkillReady(DragonSkill.Spit) && dist >= d.SpitMinRange)
            return Pick(DragonSkill.Spit, out skill);

        return false;
    }

    private static bool Pick(DragonSkill picked, out DragonSkill skill)
    {
        skill = picked;
        return true;
    }

    private float AngleToTarget()
    {
        if (boss.Target == null) return 180f;
        Vector3 to = boss.Target.transform.position - boss.transform.position;
        to.y = 0f;
        return to.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(boss.transform.forward, to);
    }

    // ⑥ 정지 거리 밖이면 걷고, 안이면 제자리에서 돈다.
    private void Approach(float time)
    {
        var d = boss.Data;
        float stop = d != null ? d.StopDistance : 6f;
        if (boss.TargetDistance > stop)
        {
            boss.PlayAnim(MonsterAnimId.Move);
            boss.ChaseStep(time);
        }
        else
        {
            boss.PlayAnim(MonsterAnimId.Idle);
            boss.FaceTargetStep(time);
        }
    }
}

/// <summary>
/// 트리앤트 트리 공격 상태. 거리와 쿨타임으로 두 기술을 고른다.
///
/// ① 나무 주먹 — 쿨이 돌아왔고 PunchRange 이내면 휘두른다. PunchHitTime 뒤에 타격 판정을 한다.
/// ② 열매 투척 — 주먹이 닿지 않는 거리에서 쿨이 돌아왔으면 던진다.
/// ③ 그 외 — 주먹 거리 밖이면 걸어서 접근하고, 안이면 제자리에서 타깃을 바라보며 주먹 쿨을 기다린다.
///
/// AttackRange를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class TreantTreeAttackState : MobState<Monster>
{
    private readonly TreantTreeStateMachine sm;
    private readonly TreantTree treant;

    private float lockTimer;    // 주먹/투척 모션 남은 시간 (>0이면 제자리 고정)
    private float punchHitTimer; // 주먹 타격까지 남은 시간
    private bool punchPending;  // 휘두르는 중이고 아직 타격 판정을 하지 않았는지

    public TreantTreeAttackState(TreantTree owner, TreantTreeStateMachine machine) : base(owner, machine)
    {
        treant = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        lockTimer = 0f;
        punchHitTimer = 0f;
        punchPending = false;
    }

    public override void Update(float time = 1.0f)
    {
        // 모션 중에는 제자리에 고정한다. 모션이 끝날 때까지 이동/사거리 판정을 하지 않는다.
        if (lockTimer > 0f)
        {
            lockTimer -= time;

            if (punchPending)
            {
                // 타격 전까지는 타깃 쪽으로 천천히 몸을 돌린다(완전히 피하려면 옆이 아니라 뒤로 빠져야 한다).
                treant.FaceTargetStep(time);

                punchHitTimer -= time;
                if (punchHitTimer <= 0f)
                {
                    punchPending = false;
                    treant.PunchHit();
                }
            }
            return;
        }

        if (!treant.IsTargetValid())
        {
            treant.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 교전 사거리(AttackRange)마저 벗어나면 다시 추적
        if (!treant.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        treant.FaceTargetStep(time);

        bool inPunchRange = treant.IsTargetInPunchRange();

        // ① 나무 주먹
        if (inPunchRange && treant.IsPunchReady)
        {
            treant.StartPunch();
            lockTimer = treant.PunchDuration;
            punchHitTimer = treant.PunchHitTime;
            punchPending = true;
            return;
        }

        // ② 열매 투척: 주먹이 닿지 않을 때만
        if (!inPunchRange && treant.IsProjectileReady)
        {
            treant.FireProjectile();
            lockTimer = treant.ProjectileCastTime;
            return;
        }

        // ③ 대기: 주먹 거리 밖이면 걸어서 접근, 안이면 제자리에서 쿨을 기다린다
        if (!inPunchRange)
        {
            treant.PlayAnim(MonsterAnimId.Move);
            treant.ChaseStep(time);
        }
        else
        {
            treant.PlayAnim(MonsterAnimId.Idle);
        }
    }
}

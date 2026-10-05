/// <summary>
/// 트리앤트 미니언 공격 상태. 세 기술을 쿨타임 우선순위로 고른다.
///
/// ① 뿌리 속박 — 쿨이 돌아왔고 SnareRange 이내면 최우선. 발을 묶어 다음 돌진을 맞히기 위한 선행기다.
/// ② 구르기 돌진 — 쿨이 돌아왔고 RollRange 이내면 돌진 상태로 전환한다. 주력 딜기.
/// ③ 도토리 투척 — 위 둘이 쿨일 때 쓰는 견제기.
/// ④ 전부 쿨이면 굴러서 접근한다(붙어 있어야 돌진 쿨이 돌아오는 즉시 때릴 수 있다).
///
/// AttackRange를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class TreantMinionAttackState : MobState<Monster>
{
    private readonly TreantMinionStateMachine sm;
    private readonly TreantMinion treant;
    private float castTimer;    // 투척/시전 모션 남은 시간 (>0이면 제자리 고정)

    public TreantMinionAttackState(TreantMinion owner, TreantMinionStateMachine machine) : base(owner, machine)
    {
        treant = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        castTimer = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        // 투척·시전 중에는 제자리에 고정한다. 모션이 끝날 때까지 이동/사거리 판정을 하지 않는다.
        // (투사체와 장판은 이미 나갔으므로 타깃이 사라져도 모션을 끝까지 재생하고 나서 다음 판단을 한다)
        if (castTimer > 0f)
        {
            castTimer -= time;
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

        // ① 뿌리 속박: 묶어두고 시작하는 것이 이 몬스터의 교전 순서다
        if (treant.IsSnareReady && treant.IsTargetInSnareRange())
        {
            treant.CastRootSnare();
            castTimer = treant.SnareCastTime;
            return;
        }

        // ② 구르기 돌진: 주력 딜기
        if (treant.IsRollReady && treant.IsTargetInRollRange())
        {
            sm.ToRoll();
            return;
        }

        // ③ 도토리 투척: 쿨 대기 중의 견제
        if (treant.IsProjectileReady)
        {
            treant.FireProjectile();
            castTimer = treant.ProjectileCastTime;
            return;
        }

        // ④ 전부 쿨: 굴러서 접근한다.
        // 투척/시전 Bool이 켜진 채로 굴러다니지 않도록 이동 애니로 되돌린다.
        treant.PlayAnim(MonsterAnimId.Move);
        treant.ChaseStep(time);
    }
}

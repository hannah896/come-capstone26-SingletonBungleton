/// <summary>
/// 데빌 지상 공격 상태. 거리로 수단을 고른다.
/// - SlashRange 이내: 할퀴기 (내부 쿨 없음, MinAttackPeriod 주기만)
/// - 그 밖 ~ AttackRange 이내: 화염구 (내부 쿨타임)
/// - 화염구 쿨 대기 중에는 걸어서 접근
///
/// 매 프레임 이륙 조건(ShouldTakeOff)을 먼저 확인한다 — 지상 전투는 어디까지나 "이륙 전 단계"다.
/// </summary>
public class DevilGroundAttackState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;

    private float slashCooldown;
    private float castTimer;    // 시전 모션 남은 시간 (>0이면 제자리 고정)

    public DevilGroundAttackState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        slashCooldown = 0f; // 사거리 진입 시 즉시 1타
        castTimer = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        // 시전 중에는 제자리에 고정한다. 이륙 판정도 미룬다(모션이 끊기지 않도록).
        if (castTimer > 0f)
        {
            castTimer -= time;
            return;
        }

        // 이륙 조건 우선 — 맞았거나, 밀착당했거나, HP 70%에 닿았거나
        if (devil.ShouldTakeOff)
        {
            sm.ToTakeOff();
            return;
        }

        if (!devil.IsTargetValid())
        {
            devil.ClearTarget();
            sm.ToIdle();
            return;
        }

        if (!devil.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        slashCooldown -= time;

        if (devil.IsTargetInSlashRange())
        {
            // 근접: 할퀴기. MinAttackPeriod 주기로만 반복
            devil.FaceTargetStep(time);
            if (slashCooldown <= 0f)
            {
                devil.PlayAnim(MonsterAnimId.Attack);
                devil.PerformAttack();
                slashCooldown = devil.MinAttackPeriod;
            }
        }
        else if (devil.IsFireballReady)
        {
            // 중거리: 화염구 1발
            devil.FaceTargetStep(time);
            devil.FireFireball();
            castTimer = devil.FireballCastTime;
        }
        else
        {
            // 화염구 쿨 대기: 걸어서 접근
            devil.PlayAnim(MonsterAnimId.Move);
            devil.ChaseStep(time);
        }
    }
}

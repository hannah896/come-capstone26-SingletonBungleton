/// <summary>
/// 데몬 공격 상태. 타깃과의 거리로 공격 수단을 고른다.
/// - SlashRange 이내: 근접 슬래시 (내부 쿨 없음, MinAttackPeriod 주기만 적용)
/// - 그 밖 ~ AttackRange 이내: 운석 세례 (긴 내부 쿨타임)
/// - 운석 세례 쿨 대기 중에는 슬래시 사거리까지 접근(도약/걷기)한다.
/// AttackRange를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class DemonAttackState : MobState<Monster>
{
    private readonly DemonStateMachine sm;
    private readonly Demon demon;
    private float slashCooldown;
    private float castTimer;    // 시전 모션 남은 시간 (>0이면 제자리 고정)

    public DemonAttackState(Demon owner, DemonStateMachine machine) : base(owner, machine)
    {
        demon = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        slashCooldown = 0f; // 슬래시 사거리 진입 시 즉시 1타
        castTimer = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        // 시전 중에는 제자리에 고정한다. 이동/추적/도약은 물론 사거리 이탈 판정도 하지 않는다.
        // (타깃이 사라져도 장판은 이미 깔렸으므로 모션을 끝까지 재생하고 나서 다음 판단을 한다)
        if (castTimer > 0f)
        {
            castTimer -= time;
            return;
        }

        if (!demon.IsTargetValid())
        {
            demon.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 운석 세례 사거리(AttackRange)마저 벗어나면 다시 추적
        if (!demon.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        slashCooldown -= time;

        if (demon.IsTargetInSlashRange())
        {
            // 근접: 슬래시. 내부 쿨 없이 MinAttackPeriod 주기로만 반복
            demon.FaceTargetStep(time);
            if (slashCooldown <= 0f)
            {
                demon.PlayAnim(MonsterAnimId.Attack);
                demon.PerformAttack();
                slashCooldown = demon.MinAttackPeriod;
            }
        }
        else if (demon.IsMeteorReady)
        {
            // 원거리: 자기 주변에 운석 세례 시전 (내부 쿨타임 시작)
            // 시전 직후 바로 움직이지 않도록 MeteorCastTime 동안 제자리에 묶는다.
            demon.FaceTargetStep(time);
            demon.CastMeteorStorm();
            castTimer = demon.MeteorCastTime;
        }
        else
        {
            // 운석 세례 쿨 대기 중: 도약이 준비됐으면 타깃으로 뛰어들고, 아니면 걸어서 접근
            if (demon.IsLeapReady)
            {
                sm.ToLeap();
            }
            else
            {
                // 시전 모션 Bool이 켜진 채로 걸어다니지 않도록 이동 애니로 되돌린다.
                demon.PlayAnim(MonsterAnimId.Move);
                demon.ChaseStep(time);
            }
        }
    }
}

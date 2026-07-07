/// <summary>
/// 미스치프 공격 상태. 타깃과의 거리로 공격 수단을 고른다.
/// - SlashRange 이내: 근접 슬래시 (내부 쿨 없음, MinAttackPeriod 주기만 적용)
/// - 그 밖 ~ AttackRange 이내: 프로젝타일 (긴 내부 쿨타임)
/// - 프로젝타일 쿨 대기 중에는 슬래시 사거리까지 접근한다.
/// AttackRange를 벗어나면 Chase, 타깃 소실 시 Idle로 전환한다.
/// </summary>
public class MischiefAttackState : MobState<Monster>
{
    private readonly MischiefStateMachine sm;
    private readonly Mischief mischief;
    private float slashCooldown;

    public MischiefAttackState(Mischief owner, MischiefStateMachine machine) : base(owner, machine)
    {
        mischief = owner;
        sm = machine;
    }

    public override void OnEnter()
    {
        slashCooldown = 0f; // 슬래시 사거리 진입 시 즉시 1타
    }

    public override void Update(float time = 1.0f)
    {
        if (!mischief.IsTargetValid())
        {
            mischief.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 프로젝타일 사거리(AttackRange)마저 벗어나면 다시 추적
        if (!mischief.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        slashCooldown -= time;

        if (mischief.IsTargetInSlashRange())
        {
            // 근접: 슬래시. 내부 쿨 없이 MinAttackPeriod 주기로만 반복
            mischief.FaceTargetStep(time);
            if (slashCooldown <= 0f)
            {
                mischief.PlayAnim(mischief.AttackAnim);
                mischief.PerformAttack();
                slashCooldown = mischief.MinAttackPeriod;
            }
        }
        else if (mischief.IsProjectileReady)
        {
            // 원거리: 프로젝타일 발사 (내부 쿨타임 시작)
            mischief.FaceTargetStep(time);
            mischief.FireProjectile();
        }
        else
        {
            // 프로젝타일 쿨 대기 중에는 슬래시 사거리까지 접근
            mischief.ChaseStep(time);
        }
    }
}

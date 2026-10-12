/// <summary>
/// 외눈 박쥐 위저드 교전 상태. 기술을 고르고, 고른 기술을 끝까지 실행한다. (BatMageAttackState와 같은 구조)
///
/// 선택 우선순위:
///   1. 스펠 준비 + 타깃이 SpellMinDistance 이상 떨어져 있음 (제 발밑에 운석을 깔지 않게)
///   2. 스핀 준비 + 스핀 범위 안에 플레이어가 있음
///   3. 근접 사거리 안 + 근접 준비 → Slash / Slice 번갈아
///   4. 근접 사거리 밖 + 마법탄 준비
///   5. 근접 사거리 밖이면 Chase 모션으로 접근, 안이면 타깃을 바라보며 대기
/// 기술 사이에는 SkillGap 동안 숨을 돌린다.
///
/// 판정 시간은 PlayAnim 호출이 아니라 애니메이터가 실제로 기술 클립에 들어간 순간부터 센다(최대 0.8s 대기).
/// 클립이 끝나면 Idle Bool로 돌려놔서 다음 기술 때 Entry에서 다시 재생되게 한다.
/// </summary>
public class BatWizardAttackState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;

    private readonly BatWizardStateMachine sm;
    private readonly BatWizard wizard;

    private MonsterAnimId skill = MonsterAnimId.None;
    private bool clipStarted;
    private float waitTimer;
    private float elapsed;
    private bool actionDone;
    private int spinHitIndex;
    private float gapTimer;

    public BatWizardAttackState(BatWizard owner, BatWizardStateMachine machine) : base(owner, machine)
    {
        wizard = owner;
        sm = machine;
    }

    // 기술 실행 중에만 경직을 막는다. (고르는 중·접근 중에는 맞으면 끊긴다)
    public override bool IsAttackState => skill != MonsterAnimId.None || wizard.IsInAttackMotion;

    public override void OnEnter()
    {
        skill = MonsterAnimId.None;
        gapTimer = 0f;
    }

    public override void Update(float time = 1.0f)
    {
        if (skill != MonsterAnimId.None)
        {
            TickSkill(time);
            return;
        }

        if (!wizard.IsTargetValid())
        {
            wizard.ClearTarget();
            sm.ToIdle();
            return;
        }

        if (!wizard.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        if (gapTimer > 0f)
        {
            gapTimer -= time;
            wizard.FaceTargetStep(time);
            return;
        }

        bool inMelee = wizard.IsTargetInMeleeRange();

        if (wizard.IsSpellReady && wizard.DistanceToTarget() >= wizard.SpellMinDistance)
        {
            Begin(MonsterAnimId.CastSpell);
            wizard.StartSpellCooldown();
        }
        else if (wizard.IsSpinReady && wizard.CountPlayersInSpin() >= 1)
        {
            Begin(MonsterAnimId.Spin);
            wizard.StartSpinCooldown();
        }
        else if (inMelee && wizard.IsMeleeReady)
        {
            Begin(wizard.NextMelee());
        }
        else if (!inMelee && wizard.IsProjectileReady)
        {
            Begin(MonsterAnimId.RangeAttack);
            wizard.StartProjectileCooldown();
        }
        else if (!inMelee)
        {
            wizard.PlayAnim(MonsterAnimId.Chase);
            wizard.ChaseStep(time);
        }
        else
        {
            wizard.PlayAnim(MonsterAnimId.Idle);
            wizard.FaceTargetStep(time);
        }
    }

    private void Begin(MonsterAnimId kind)
    {
        skill = kind;
        clipStarted = false;
        waitTimer = MaxClipWait;
        elapsed = 0f;
        actionDone = false;
        spinHitIndex = 0;
        wizard.PlayAnim(kind);
    }

    private void TickSkill(float time)
    {
        wizard.FaceTargetStep(time);

        // 1. 애니메이터가 기술 클립에 들어갈 때까지 대기
        if (!clipStarted)
        {
            waitTimer -= time;
            if (wizard.IsInSkillClip(skill) || waitTimer <= 0f)
                clipStarted = true;
            return;
        }

        // 2. 클립 기준 시간으로 판정
        elapsed += time;

        switch (skill)
        {
            case MonsterAnimId.Attack:
            case MonsterAnimId.Slice:
                if (!actionDone && elapsed >= wizard.MeleeHitTime)
                {
                    actionDone = true;
                    wizard.MeleeHit();
                }
                if (elapsed >= wizard.MeleeDuration) End();
                break;

            case MonsterAnimId.Spin:
                var times = wizard.SpinHitTimes;
                while (spinHitIndex < times.Length && elapsed >= times[spinHitIndex])
                {
                    wizard.SpinHit(last: spinHitIndex == times.Length - 1);
                    spinHitIndex++;
                }
                if (elapsed >= wizard.SpinDuration) End();
                break;

            case MonsterAnimId.RangeAttack:
                if (!actionDone && elapsed >= wizard.ProjectileReleaseTime)
                {
                    actionDone = true;
                    wizard.FireProjectile();
                }
                if (elapsed >= wizard.ProjectileDuration) End();
                break;

            case MonsterAnimId.CastSpell:
                if (!actionDone && elapsed >= wizard.SpellCastPoint)
                {
                    actionDone = true;
                    wizard.CastMeteors();
                }
                if (elapsed >= wizard.SpellDuration) End();
                break;

            default:
                End();
                break;
        }
    }

    private void End()
    {
        skill = MonsterAnimId.None;
        gapTimer = wizard.SkillGap;
        wizard.PlayAnim(MonsterAnimId.Idle);
    }
}

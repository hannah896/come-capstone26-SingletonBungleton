/// <summary>
/// 외눈 박쥐 메이지 교전 상태. 기술을 고르고, 고른 기술을 끝까지 실행한다.
///
/// 선택 우선순위 (쿨이 긴 큰 기술부터):
///   0. 회복 준비 + 반경 안에 체력 HealTriggerHpRatio 이하인 아군 몬스터가 있음
///   1. 스펠 준비 (AttackRange 안 어디서든)
///   2. 스핀 준비 + 범위 안에 플레이어가 있음
///   3. 슬래시 사거리 안 + 슬래시 준비
///   4. 슬래시 사거리 밖이면 Chase 모션으로 접근, 안이면 타깃을 바라보며 대기
/// 기술 사이에는 SkillGap 동안 숨을 돌린다.
///
/// 판정 시간은 PlayAnim 호출이 아니라 애니메이터가 실제로 기술 클립에 들어간 순간부터 센다(최대 0.8s 대기).
/// 클립이 끝나면 Idle Bool로 돌려놔서 다음 기술 때 Entry에서 다시 재생되게 한다.
/// </summary>
public class BatMageAttackState : MobState<Monster>
{
    // 클립 진입을 기다리는 최대 시간. 애니메이터가 끝내 안 들어가도 기술은 진행된다.
    private const float MaxClipWait = 0.8f;

    private readonly BatMageStateMachine sm;
    private readonly BatMage mage;

    private MonsterAnimId skill = MonsterAnimId.None; // 실행 중인 기술 (None이면 고르는 중)
    private bool clipStarted;
    private float waitTimer;
    private float elapsed;
    private bool actionDone;   // 슬래시 타격 / 스펠 시전이 이미 나갔는지
    private int spinHitIndex;
    private float gapTimer;

    public BatMageAttackState(BatMage owner, BatMageStateMachine machine) : base(owner, machine)
    {
        mage = owner;
        sm = machine;
    }

    // 기술 실행 중에만 경직을 막는다. (고르는 중·접근 중에는 경직 쿨이 허락하면 끊긴다)
    public override bool IsAttackState => skill != MonsterAnimId.None || mage.IsInAttackMotion;

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

        if (!mage.IsTargetValid())
        {
            mage.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 스펠 사거리(AttackRange) 밖이면 다시 추적
        if (!mage.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        // 기술 사이 숨 돌리기: 타깃만 바라본다
        if (gapTimer > 0f)
        {
            gapTimer -= time;
            mage.FaceTargetStep(time);
            return;
        }

        // 쿨이 돌아온 큰 기술부터 쓴다. 슬래시는 쿨이 짧아 항상 준비돼 있으므로 맨 뒤에 둬야
        // 스펠·스핀이 끼어들 수 있다.
        if (mage.IsHealReady && mage.CollectHealTargets())
        {
            Begin(MonsterAnimId.Heal);
            mage.StartHealCooldown();
        }
        else if (mage.IsSpellReady)
        {
            Begin(MonsterAnimId.CastSpell);
            mage.StartSpellCooldown();
        }
        else if (mage.IsSpinReady && mage.CountPlayersInSpin() >= 1)
        {
            Begin(MonsterAnimId.Spin);
            mage.StartSpinCooldown();
        }
        else if (mage.IsTargetInSlashRange() && mage.IsSlashReady)
        {
            Begin(MonsterAnimId.Attack);
            mage.StartSlashCooldown();
        }
        else if (!mage.IsTargetInSlashRange())
        {
            mage.PlayAnim(MonsterAnimId.Chase);
            mage.ChaseStep(time);
        }
        else
        {
            mage.PlayAnim(MonsterAnimId.Idle);
            mage.FaceTargetStep(time);
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
        mage.PlayAnim(kind);
    }

    private void TickSkill(float time)
    {
        // 스핀은 회전하며 다가가고, 나머지는 제자리에서 타깃을 바라본다
        if (skill == MonsterAnimId.Spin) mage.SpinMoveStep(time);
        else mage.FaceTargetStep(time);

        // 1. 애니메이터가 기술 클립에 들어갈 때까지 대기
        if (!clipStarted)
        {
            waitTimer -= time;
            if (mage.IsInSkillClip(skill) || waitTimer <= 0f)
                clipStarted = true;
            return;
        }

        // 2. 클립 기준 시간으로 판정
        elapsed += time;

        switch (skill)
        {
            case MonsterAnimId.Attack:
                if (!actionDone && elapsed >= mage.SlashHitTime)
                {
                    actionDone = true;
                    mage.SlashHit();
                }
                if (elapsed >= mage.SlashDuration) End();
                break;

            case MonsterAnimId.Spin:
                var times = mage.SpinHitTimes;
                while (spinHitIndex < times.Length && elapsed >= times[spinHitIndex])
                {
                    mage.SpinHit(last: spinHitIndex == times.Length - 1);
                    spinHitIndex++;
                }
                if (elapsed >= mage.SpinDuration) End();
                break;

            case MonsterAnimId.CastSpell:
                if (!actionDone && elapsed >= mage.SpellCastPoint)
                {
                    actionDone = true;
                    mage.CastStars();
                }
                if (elapsed >= mage.SpellDuration) End();
                break;

            case MonsterAnimId.Heal:
                if (!actionDone && elapsed >= mage.HealCastPoint)
                {
                    actionDone = true;
                    mage.CastHeal();
                }
                if (elapsed >= mage.HealDuration) End();
                break;

            default:
                End();
                break;
        }
    }

    private void End()
    {
        skill = MonsterAnimId.None;
        gapTimer = mage.SkillGap;
        mage.PlayAnim(MonsterAnimId.Idle); // 비루프 기술 클립(스핀은 루프)을 끝내고 대기로
    }
}

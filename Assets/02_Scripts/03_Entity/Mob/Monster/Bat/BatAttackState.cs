/// <summary>
/// 외눈 박쥐 공격 상태. 타깃과의 거리로 공격 수단을 고른다.
/// - BiteRange 이내: 물기 (MinAttackPeriod 주기). 클립 시작 후 BiteHitTime에 판정
/// - 그 밖 ~ AttackRange 이내: 눈알 탄 (내부 쿨타임). 클립 시작 후 ProjectileReleaseTime에 발사
/// - 눈알 탄 쿨 대기 중에는 물기 사거리까지 날아서 접근한다
/// 모션 동안은 제자리에서 타깃만 바라본다. AttackRange를 벗어나면 Chase, 타깃 소실 시 Idle.
///
/// 판정 시간은 PlayAnim 호출이 아니라 "애니메이터가 실제로 공격 클립에 들어간 순간"부터 센다.
/// Bool → Exit → Entry 경로라 클립 시작까지 지연이 있고(스폰 반복 중이면 더 길다), 호출 기준으로 세면
/// 모션이 나오기 전에 데미지가 들어간다.
///
/// 공격 클립은 루프가 아니므로, 모션이 끝나면 Idle Bool로 돌려놓아야 다음 공격 때 Entry에서 다시 재생된다.
/// </summary>
public class BatAttackState : MobState<Monster>
{
    private enum Pending { None, Bite, Projectile }

    // 클립 진입을 기다리는 최대 시간. 애니메이터가 끝내 안 들어가도(파라미터 누락 등) 공격은 진행된다.
    private const float MaxClipWait = 0.8f;

    private readonly BatStateMachine sm;
    private readonly Bat bat;

    private Pending pending;       // 진행 중인 공격 (None이면 모션 중 아님)
    private bool clipStarted;      // 애니메이터가 공격 클립에 들어갔는지
    private float waitTimer;       // 클립 진입 대기 남은 시간
    private float actionTimer;     // 클립 시작 후 경과 시간
    private float hitTime;         // 판정/발사 시점 (클립 기준)
    private float duration;        // 모션 길이 (클립 기준)
    private bool hitApplied;
    private float biteCooldown;

    public BatAttackState(Bat owner, BatStateMachine machine) : base(owner, machine)
    {
        bat = owner;
        sm = machine;
    }

    // 교전 상태 전체가 아니라 실제 공격 모션 중일 때만 Hit를 막는다. (쿨다운·접근 중에는 Hit로 끊긴다)
    public override bool IsAttackState => pending != Pending.None || bat.IsInAttackMotion;

    public override void OnEnter()
    {
        pending = Pending.None;
        biteCooldown = 0f; // 물기 사거리 진입 시 즉시 1타
    }

    public override void Update(float time = 1.0f)
    {
        biteCooldown -= time;

        // 공격 모션 중: 타깃을 바라보며 판정/발사 타이밍만 기다린다
        if (pending != Pending.None)
        {
            bat.FaceTargetStep(time);
            TickAction(time);
            return;
        }

        if (!bat.IsTargetValid())
        {
            bat.ClearTarget();
            sm.ToIdle();
            return;
        }

        // 눈알 탄 사거리(AttackRange)마저 벗어나면 다시 추적
        if (!bat.IsTargetInAttackRange())
        {
            sm.ToChase();
            return;
        }

        if (bat.IsTargetInBiteRange())
        {
            bat.FaceTargetStep(time);
            if (biteCooldown <= 0f)
                StartBite();
            else
                bat.PlayAnim(MonsterAnimId.Idle);
        }
        else if (bat.IsProjectileReady)
        {
            StartProjectile();
        }
        else
        {
            // 눈알 탄 쿨 대기 중: 물기 사거리까지 날아서 접근
            bat.PlayAnim(MonsterAnimId.Move);
            bat.ChaseStep(time);
        }
    }

    private void StartBite()
    {
        bat.PlayAnim(MonsterAnimId.Attack);
        biteCooldown = bat.MinAttackPeriod;
        Begin(Pending.Bite, bat.BiteHitTime, bat.BiteDuration);
    }

    private void StartProjectile()
    {
        bat.PlayAnim(MonsterAnimId.RangeAttack);
        bat.StartProjectileCooldown();
        Begin(Pending.Projectile, bat.ProjectileReleaseTime, bat.ProjectileCastTime);
    }

    private void Begin(Pending kind, float hit, float length)
    {
        pending = kind;
        hitTime = hit;
        duration = length;
        clipStarted = false;
        waitTimer = MaxClipWait;
        actionTimer = 0f;
        hitApplied = false;
    }

    private void TickAction(float time)
    {
        // 1. 애니메이터가 공격 클립에 들어갈 때까지 대기
        if (!clipStarted)
        {
            waitTimer -= time;
            if (bat.IsInAttackClip(pending == Pending.Projectile) || waitTimer <= 0f)
                clipStarted = true;
            return;
        }

        // 2. 클립 기준으로 판정/발사, 끝나면 날갯짓(Idle)으로
        actionTimer += time;

        if (!hitApplied && actionTimer >= hitTime)
        {
            hitApplied = true;
            if (pending == Pending.Bite) bat.BiteHit();
            else bat.FireProjectile();
        }

        if (actionTimer >= duration)
        {
            pending = Pending.None;
            bat.PlayAnim(MonsterAnimId.Idle); // 비루프 공격 클립을 끝내고 날갯짓으로
        }
    }
}

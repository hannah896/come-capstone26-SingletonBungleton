using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>데빌의 자세(Stance). 잠복(Underground)은 P3 예정.</summary>
public enum DevilStance
{
    Ground,
    Fly,
}

/// <summary>
/// 임프 데빌. (기획: docs/기획_데빌_스킬로직.md)
///
/// "때리면 날아오르고, 몰리면 땅으로 숨는다" — 정면 승부를 하지 않는 몬스터.
/// 지상에서는 평범하지만, 피격이 쌓이거나 플레이어가 밀착하면 이륙해 근접을 무효화하고 강한 기술을 쓴다.
///
/// 자세 전환 판정과 쿨타임은 이 본체가, 자세별 스킬 선택은 DevilGroundAttackState / DevilFlyAttackState가 담당한다.
/// 비행은 루트 Transform Y를 FlyHeight만큼 올려서 구현한다 — 콜라이더가 함께 올라가므로
/// 플레이어의 근접 공격이 "논리적 무적"이 아니라 물리적으로 닿지 않게 된다.
///
/// 진짜 찾기 (만월 보스 패턴): 체력이 CloneHpRatios 아래로 내려가면 연기와 함께 사라졌다가
/// 플레이어를 둘러싼 원 위에 진짜 1 + 분신 CloneCount마리로 나타난다.
/// - 분신은 같은 프리팹·같은 AI지만 피해가 약하고, 맞아도 죽지 않고 연기를 내며 다른 자리로 옮겨 간다
/// - 체력바는 진짜와 같은 비율로 보인다 (진짜만 체력바가 떠서 들키지 않게)
/// - 단서: 분신에는 그림자가 없다. 패턴 중에는 진짜도 날아오르지 않는다
/// - 진짜를 맞히면 분신이 모두 사라지고 진짜가 CloneRevealStunTime 동안 비틀거린다. 못 찾으면 CloneDuration 뒤 저절로 사라진다
/// </summary>
public class Devil : Monster
{
    #region Inspector
    [Header("데빌 - 애니메이션 Bool 파라미터명 (컨트롤러 기준, 없으면 비워둠)")]
    [Tooltip("지상 화염구 (Projectile Attack)")]
    [SerializeField] private string rangeAttackBool = "";
    [Tooltip("비행 대기 (Fly Idle)")]
    [SerializeField] private string flyIdleBool = "";
    [Tooltip("비행 이동 (Fly Forward In Place)")]
    [SerializeField] private string flyMoveBool = "";
    [Tooltip("급강하 할퀴기 (Fly Slash Attack)")]
    [SerializeField] private string flyAttackBool = "";
    [Tooltip("화염 연사 (Fly Projectile Attack)")]
    [SerializeField] private string flyRangeAttackBool = "";
    [Tooltip("꼬리치기 (Fly Tail Attack)")]
    [SerializeField] private string flyTailBool = "";
    [Tooltip("지옥 세례 (Fly Cast Spell)")]
    [SerializeField] private string flyCastBool = "";
    [Tooltip("이륙 전환 (Idle To Fly Idle)")]
    [SerializeField] private string takeOffBool = "";

    [Header("데빌 - 착지 지면 탐색")]
    [Tooltip("착지 시 지면을 찾을 레이어. 못 찾으면 이륙 지점의 높이로 되돌아간다")]
    [SerializeField] private LayerMask groundMask = 0;
    #endregion

    private int rangeAttackBoolHash;
    private int flyIdleBoolHash;
    private int flyMoveBoolHash;
    private int flyAttackBoolHash;
    private int flyRangeAttackBoolHash;
    private int flyTailBoolHash;
    private int flyCastBoolHash;
    private int takeOffBoolHash;

    // 쿨타임 (자세와 무관하게 항상 감소한다)
    private float fireballCooldown;
    private float takeOffCooldown;
    private float diveCooldown;
    private float tailCooldown;
    private float flyProjectileCooldown;
    private float hellRainCooldown;

    // 자세 전환 트래킹
    private float flyRemaining;      // 남은 비행 시간
    private int hitCount;            // 이륙 유발 피격 누적
    private float nearContactTimer;  // 플레이어 밀착 지속 시간
    private bool phase2Triggered;    // HP 70% 강제 이륙 1회 소진 여부
    private float groundY;           // 이륙 직전의 지면 높이 (착지 폴백)

    private DevilStatData DevilData => statData as DevilStatData;

    // 진짜 찾기 (분신)
    private readonly List<Devil> clones = new List<Devil>();
    private int cloneThresholdIndex;   // 다음에 볼 CloneHpRatios 인덱스
    private bool clonePending;
    private float clonePhaseTimer;     // >0이면 분신 페이즈 진행 중 (진짜에서만)
    private Devil cloneOwner;          // 분신이면 진짜, 아니면 null
    private Player cloneCenter;        // 분신 페이즈를 시작할 때 둘러싼 플레이어 (분신이 옮겨 갈 원의 중심)
    private Renderer[] modelRenderers;
    private ShadowCastingMode[] originalShadows;

    #region Properties (상태 클래스에서 사용)
    public DevilStance Stance { get; private set; } = DevilStance.Ground;
    public bool IsFlying => Stance == DevilStance.Fly;
    /// <summary>비행 중에는 지상 배회(걷기)를 하지 않는다.</summary>
    public override bool CanWander => !IsFlying && base.CanWander;

    public float SlashRange => DevilData != null ? DevilData.SlashRange : 2f;
    public float FireballCastTime => DevilData != null ? DevilData.FireballCastTime : 0.8f;
    public bool IsFireballReady => fireballCooldown <= 0f;

    public float FlyHeight => DevilData != null ? DevilData.FlyHeight : 3.5f;
    public float TakeOffDuration => DevilData != null ? DevilData.TakeOffDuration : 0.8f;
    public float LandDuration => DevilData != null ? DevilData.LandDuration : 0.5f;
    public float FlyMoveSpeedMultiplier => DevilData != null ? DevilData.FlyMoveSpeedMultiplier : 1.2f;

    public float DiveRange => DevilData != null ? DevilData.DiveRange : 6f;
    public float DiveDuration => DevilData != null ? DevilData.DiveDuration : 0.5f;
    public bool IsDiveReady => diveCooldown <= 0f;

    public float TailRange => DevilData != null ? DevilData.TailRange : 3f;
    public float TailCastTime => DevilData != null ? DevilData.TailCastTime : 0.6f;
    public bool IsTailReady => tailCooldown <= 0f;

    public float FlyProjectileCastTime => DevilData != null ? DevilData.FlyProjectileCastTime : 0.8f;
    public bool IsFlyProjectileReady => flyProjectileCooldown <= 0f;

    public float HellRainCastTime => DevilData != null ? DevilData.HellRainCastTime : 1.2f;
    public bool IsHellRainReady => hellRainCooldown <= 0f;

    /// <summary>비행 지속 시간이 끝났는지. 착지 판단에 사용한다.</summary>
    public bool IsFlyTimeOver => flyRemaining <= 0f;

    /// <summary>지상에서 이륙 조건을 만족했는지. 착지 쿨타임이 남아 있으면 항상 false.</summary>
    public bool ShouldTakeOff
    {
        get
        {
            if (IsFlying || takeOffCooldown > 0f || DevilData == null) return false;
            // 분신은 날지 않고, 진짜도 분신 페이즈 중에는 땅에 남는다 (날아오르면 진짜가 들킨다)
            if (IsClone || IsClonePhaseActive) return false;

            // ① 피격 누적  ② 플레이어 밀착 지속  ③ HP 70% 최초 도달(강제 1회)
            if (hitCount >= DevilData.HitsToTakeOff) return true;
            if (nearContactTimer >= DevilData.NearContactTime) return true;
            if (!phase2Triggered && HpRatio <= DevilData.Phase2HpRatio) return true;

            return false;
        }
    }
    // 체력 비율은 베이스 Monster.HpRatio를 쓴다 (호스트에서는 status 기준으로 같은 값)

    /// <summary>진짜 찾기 패턴의 분신인지.</summary>
    public bool IsClone => cloneOwner != null;
    /// <summary>분신 페이즈 진행 중인지 (진짜 기준).</summary>
    public bool IsClonePhaseActive => clonePhaseTimer > 0f;
    /// <summary>분신 시전 대기 중인지. 땅에 있을 때만 시전한다.</summary>
    public bool IsClonePending => clonePending && !IsClone && !IsClonePhaseActive;
    public float CloneVanishTime => DevilData != null ? DevilData.CloneVanishTime : 0.5f;
    public float CloneAppearTime => DevilData != null ? DevilData.CloneAppearTime : 0.9f;
    public float CloneRevealStunTime => DevilData != null ? DevilData.CloneRevealStunTime : 3f;

    // 분신은 약하게 때린다 (기본 할퀴기에 적용)
    protected override float OutgoingDamageMultiplier
        => IsClone && DevilData != null ? DevilData.CloneDamageMultiplier : 1f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        flyIdleBoolHash = ToAnimHash(flyIdleBool);
        flyMoveBoolHash = ToAnimHash(flyMoveBool);
        flyAttackBoolHash = ToAnimHash(flyAttackBool);
        flyRangeAttackBoolHash = ToAnimHash(flyRangeAttackBool);
        flyTailBoolHash = ToAnimHash(flyTailBool);
        flyCastBoolHash = ToAnimHash(flyCastBool);
        takeOffBoolHash = ToAnimHash(takeOffBool);

        // 분신은 그림자를 끈다 (눈치 빠른 플레이어를 위한 단서). 풀 재사용 시 되돌릴 원래 값을 기억한다
        modelRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalShadows = new ShadowCastingMode[modelRenderers.Length];
        for (int i = 0; i < modelRenderers.Length; i++)
            originalShadows[i] = modelRenderers[i].shadowCastingMode;
    }

    // 풀에서 재사용될 때 자세와 모든 쿨타임을 초기 상태로 되돌린다.
    public override void OnSpawn()
    {
        base.OnSpawn();

        Stance = DevilStance.Ground;
        fireballCooldown = 0f;
        takeOffCooldown = 0f;
        diveCooldown = 0f;
        tailCooldown = 0f;
        flyProjectileCooldown = 0f;
        hellRainCooldown = 0f;
        flyRemaining = 0f;
        hitCount = 0;
        nearContactTimer = 0f;
        phase2Triggered = false;
        groundY = transform.position.y;

        // 풀에서 나온 개체는 항상 진짜로 시작한다 (분신 설정은 SetupAsClone이 다시 건다)
        cloneOwner = null;
        clones.Clear();
        cloneThresholdIndex = 0;
        clonePending = false;
        clonePhaseTimer = 0f;
        SetShadows(true);
        SetHidden(false);
    }

    public override void OnDespawn()
    {
        if (IsClone)
        {
            cloneOwner.clones.Remove(this);
            cloneOwner = null;
        }
        else
        {
            EndClonePhase(found: false);
        }
        SetShadows(true);
        SetHidden(false);
        base.OnDespawn();
    }

    protected override MonsterStateMachine CreateStateMachine()
        => new DevilStateMachine(this);

    // 데빌 전용 애니를 매핑에 더한다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack    => rangeAttackBoolHash,
        MonsterAnimId.FlyIdle        => flyIdleBoolHash,
        MonsterAnimId.FlyMove        => flyMoveBoolHash,
        MonsterAnimId.FlyAttack      => flyAttackBoolHash,
        MonsterAnimId.FlyRangeAttack => flyRangeAttackBoolHash,
        MonsterAnimId.FlyTail        => flyTailBoolHash,
        MonsterAnimId.FlyCast        => flyCastBoolHash,
        MonsterAnimId.TakeOff        => takeOffBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 쿨타임과 자세 전환 트래킹은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (IsSimulatedPeer)
        {
            TickCooldowns(deltaTime);
            TickStanceTracking(deltaTime);
            TickClones(deltaTime);
        }

        base.OnGameUpdate(deltaTime);
    }

    private void TickCooldowns(float deltaTime)
    {
        if (fireballCooldown > 0f) fireballCooldown -= deltaTime;
        if (takeOffCooldown > 0f) takeOffCooldown -= deltaTime;
        if (diveCooldown > 0f) diveCooldown -= deltaTime;
        if (tailCooldown > 0f) tailCooldown -= deltaTime;
        if (flyProjectileCooldown > 0f) flyProjectileCooldown -= deltaTime;
        if (hellRainCooldown > 0f) hellRainCooldown -= deltaTime;
    }

    private void TickStanceTracking(float deltaTime)
    {
        if (IsFlying)
        {
            flyRemaining -= deltaTime;
            nearContactTimer = 0f;
            return;
        }

        // 지상: 플레이어가 얼마나 오래 밀착해 있는지 누적한다.
        if (DevilData != null && Target != null && PlanarDistanceToTarget() <= DevilData.NearContactRange)
            nearContactTimer += deltaTime;
        else
            nearContactTimer = 0f;
    }

    #region 피격 — 이륙 트리거
    /// <summary>
    /// 피격 누적을 세어 이륙 조건 ①을 만든다.
    /// ※ 근접/원거리 구분은 하지 않는다. DamageContext.ActionType은 채집용 동사(Pick/Mine/Chop…)만 있고
    ///   전투용 근접/원거리 구분이 없으며, 플레이어의 원거리 무기(F-CMB-006 활)도 미구현이다.
    ///   격추(Shoot-down) 메카닉은 그 둘이 갖춰진 뒤에 붙인다. (기획서 P2)
    /// </summary>
    public override void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

        // 분신: 피해를 받지 않고 연기를 내며 다른 자리로 옮겨 간다
        if (IsClone)
        {
            cloneOwner.OnCloneStruck(this);
            return;
        }

        bool revealed = IsClonePhaseActive;
        base.ApplyDamage(context);

        if (!IsFlying)
            hitCount++;

        // 진짜를 찾았다: 분신이 모두 사라지고 비틀거린다
        if (revealed && status != null && !status.IsDead)
        {
            EndClonePhase(found: true);
            (stateMachine as DevilStateMachine)?.ToCloneReveal();
        }
    }
    #endregion

    /// <summary>
    /// 비행 중 사망하면 시체와 드롭이 공중에 뜬 채로 남는다. 죽는 순간 지면 높이로 내려놓는다.
    /// (드롭은 FinishDeath에서 transform.position 기준으로 스폰된다)
    /// </summary>
    protected override void OnDeath()
    {
        EndClonePhase(found: false);

        if (IsFlying)
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, FindLandingY(), pos.z);
            Stance = DevilStance.Ground;
        }

        base.OnDeath();
    }

    #region 자세 전환
    /// <summary>이륙 확정. 비행 지속 시간을 채우고 트리거 누적을 리셋한다.</summary>
    public void EnterFlyStance()
    {
        Stance = DevilStance.Fly;
        flyRemaining = DevilData != null ? DevilData.FlyDuration : 12f;
        hitCount = 0;
        nearContactTimer = 0f;

        // HP 70% 강제 이륙은 1회만 발동한다.
        if (DevilData != null && HpRatio <= DevilData.Phase2HpRatio)
            phase2Triggered = true;
    }

    /// <summary>착지 확정. 재이륙 쿨타임을 걸어 플레이어의 딜 타임을 보장한다.</summary>
    public void EnterGroundStance()
    {
        Stance = DevilStance.Ground;
        flyRemaining = 0f;
        hitCount = 0;
        nearContactTimer = 0f;
        takeOffCooldown = DevilData != null ? DevilData.TakeOffCooldown : 8f;
    }

    /// <summary>이륙 직전의 지면 높이를 기억해 둔다(착지 레이캐스트 실패 시 폴백).</summary>
    public void RememberGroundY() => groundY = transform.position.y;

    /// <summary>
    /// 현재 XZ 위치의 착지 높이를 구한다.
    /// 비행 중 이동해 지형이 달라졌을 수 있으므로 아래로 레이캐스트하고, 실패하면 이륙 지점 높이를 쓴다.
    /// </summary>
    public float FindLandingY()
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        float distance = FlyHeight + 50f;
        int mask = groundMask.value != 0 ? groundMask.value : ~0;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return groundY;
    }

    /// <summary>비행 중 수평 이동. 지상 ChaseStep과 달리 Y를 유지한 채 비행 속도로 접근한다.</summary>
    public void FlyChaseStep(float deltaTime)
    {
        if (Target == null || status == null) return;

        Vector3 dir = Target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector3 nd = dir.normalized;
        FaceTargetStep(deltaTime);
        transform.position += nd * (status.MoveSpeed * FlyMoveSpeedMultiplier * deltaTime);
    }
    #endregion

    #region 진짜 찾기 (분신)
    private void TickClones(float deltaTime)
    {
        var d = DevilData;
        if (d == null) return;

        if (IsClone)
        {
            // 진짜가 사라졌으면 함께 사라진다. 체력바가 진짜와 같은 비율로 보이도록 맞춘다
            if (cloneOwner == null || !cloneOwner.isActiveAndEnabled || cloneOwner.Status == null || cloneOwner.Status.IsDead)
            {
                Vanish();
                return;
            }
            if (status != null) status.SetCurrentHp(cloneOwner.HpRatio * status.MaxHp);
            if (cloneOwner.Target != null && cloneOwner.Target != Target) SetTarget(cloneOwner.Target);
            return;
        }

        // 체력 기준선을 넘으면 시전 대기 (한 번에 여러 줄을 넘어도 한 번만)
        if (d.CloneHpRatios != null && status != null && !status.IsDead)
        {
            while (cloneThresholdIndex < d.CloneHpRatios.Length && HpRatio <= d.CloneHpRatios[cloneThresholdIndex])
            {
                cloneThresholdIndex++;
                clonePending = true;
            }
        }

        if (clonePhaseTimer > 0f)
        {
            clonePhaseTimer -= deltaTime;
            if (clonePhaseTimer <= 0f) EndClonePhase(found: false); // 못 찾았다: 저절로 사라진다
        }
    }

    /// <summary>분신 시전 시작 (DevilCloneCastState가 호출).</summary>
    public void ConsumeClonePending() => clonePending = false;

    /// <summary>모델을 숨기거나 보인다 (연기 속으로 사라지는 연출).</summary>
    public void SetHidden(bool hidden)
    {
        if (modelRenderers == null) return;
        foreach (var r in modelRenderers)
            if (r != null) r.enabled = !hidden;
    }

    private void SetShadows(bool on)
    {
        if (modelRenderers == null) return;
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            if (modelRenderers[i] == null) continue;
            modelRenderers[i].shadowCastingMode = on ? originalShadows[i] : ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// 분신 페이즈 시작: 플레이어를 둘러싼 원 위에 진짜와 분신을 고르게 세운다. 진짜 자리는 무작위.
    /// </summary>
    public void BeginClonePhase()
    {
        var d = DevilData;
        if (d == null || Target == null) return;

        cloneCenter = Target;
        int total = Mathf.Max(d.CloneCount, 0) + 1;
        var spots = CloneSpots(Target.transform.position, total, d.CloneRadius);
        int realIndex = Random.Range(0, total);

        PlayPoof(transform.position);
        PlaceAt(this, spots[realIndex], Target.transform.position);
        PlayPoof(transform.position);

        // 분신 스폰이 풀 캐시로 바로 끝날 수 있으므로 페이즈를 먼저 켠다 (안 그러면 방금 만든 분신을 스스로 치운다)
        clonePhaseTimer = d.CloneDuration;

        string key = Main.Pool.GetAddress(gameObject);
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[Devil] 풀 주소를 몰라 분신을 만들 수 없습니다.", this);
            return;
        }
        for (int i = 0; i < total; i++)
        {
            if (i == realIndex) continue;
            SpawnCloneAsync(key, spots[i]).Forget();
        }
    }

    private async UniTaskVoid SpawnCloneAsync(string key, Vector3 position)
    {
        var monster = await Monster.SpawnAsync(key, statData as MonsterStatData, position);
        if (monster is not Devil clone)
        {
            if (monster != null) Extensions.Despawn(monster.gameObject);
            Debug.LogWarning($"[Devil] 분신 스폰 실패 ({key})", this);
            return;
        }

        // 로드 중에 페이즈가 끝났으면 바로 치운다
        if (!IsClonePhaseActive || status == null || status.IsDead || !isActiveAndEnabled)
        {
            Extensions.Despawn(clone.gameObject);
            return;
        }

        clone.SetupAsClone(this);
        clones.Add(clone);
        PlayPoof(position);

#if PHOTON_FUSION
        // 멀티: 분신도 복제 대상으로 등록한다 (클라에는 그림자 단서가 없다 — 그림자는 이 피어에서만 끈다)
        var catalog = MonsterCatalog.Instance;
        byte id = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
        if (id != 0 && Main.Network != null && Main.Network.IsInRoom)
            NetworkMonsterDirector.Instance?.RegisterMonster(clone, id);
#endif
    }

    private void SetupAsClone(Devil owner)
    {
        cloneOwner = owner;
        SetShadows(false);
        if (owner.Target != null)
        {
            SetTarget(owner.Target);
            PlaceAt(this, transform.position, owner.Target.transform.position);
        }
        if (status != null) status.SetCurrentHp(owner.HpRatio * status.MaxHp);
        (stateMachine as DevilStateMachine)?.ToAttack();
    }

    /// <summary>분신이 맞았다: 연기를 내며 잠깐 뒤 원 위의 다른 자리로 옮겨 간다.</summary>
    private void OnCloneStruck(Devil clone)
    {
        clone.GetComponent<MobHitFlash>()?.Flash();
        clone.BlinkAsync().Forget();
    }

    private async UniTaskVoid BlinkAsync()
    {
        var d = DevilData;
        float delay = d != null ? d.CloneBlinkDelay : 0.25f;
        PlayPoof(transform.position);
        SetHidden(true);
        await UniTask.Delay(Mathf.RoundToInt(delay * 1000f), DelayType.DeltaTime, cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (!isActiveAndEnabled || !IsClone) return;

        // 진짜가 타깃을 잠깐 놓쳤어도 처음 둘러쌌던 플레이어를 기준으로 옮긴다
        Player target = cloneOwner.Target != null ? cloneOwner.Target : cloneOwner.cloneCenter;
        if (target != null && d != null)
        {
            float angle = Random.Range(0f, 360f);
            Vector3 pos = target.transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * d.CloneRadius;
            PlaceAt(this, pos, target.transform.position);
        }
        SetHidden(false);
        PlayPoof(transform.position);
    }

    /// <summary>분신 페이즈를 끝낸다. 분신은 모두 연기로 사라진다.</summary>
    public void EndClonePhase(bool found)
    {
        clonePhaseTimer = 0f;
        if (clones.Count == 0) return;

        var list = new List<Devil>(clones);
        clones.Clear();
        foreach (var clone in list)
            if (clone != null && clone.isActiveAndEnabled) clone.Vanish();
    }

    private void Vanish()
    {
        PlayPoof(transform.position);
        if (cloneOwner != null) cloneOwner.clones.Remove(this);
        cloneOwner = null;
        Extensions.Despawn(gameObject);
    }

    private static List<Vector3> CloneSpots(Vector3 center, int count, float radius)
    {
        var spots = new List<Vector3>(count);
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = start + 360f * i / count;
            spots.Add(center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius);
        }
        return spots;
    }

    // 지면에 세우고 플레이어를 바라보게 한다
    private static void PlaceAt(Devil devil, Vector3 pos, Vector3 lookAt)
    {
        if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Monster>() == null && hit.collider.GetComponentInParent<Player>() == null)
            pos.y = hit.point.y;
        else
            pos.y = lookAt.y;

        Vector3 look = lookAt - pos;
        look.y = 0f;
        devil.transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : devil.transform.rotation);
    }

    public void PlayPoof(Vector3 position)
    {
        var d = DevilData;
        if (d == null || string.IsNullOrEmpty(d.CloneVfxKey)) return;
        PlayPoofAsync(d.CloneVfxKey, position + Vector3.up * 0.8f).Forget();
    }

    private static async UniTaskVoid PlayPoofAsync(string key, Vector3 position)
    {
        var vfx = await Extensions.SpawnAsync<DragonVfx>(key);
        if (vfx == null) return;
        vfx.Play(position, 1.2f);
    }
    #endregion

    #region 스킬 — 지상
    /// <summary>타깃이 할퀴기 사거리 안에 있는지.</summary>
    public bool IsTargetInSlashRange()
        => Target != null && PlanarDistanceToTarget() <= SlashRange;

    /// <summary>지상 화염구 1발. 쿨타임 중이거나 타깃이 없으면 무시된다.</summary>
    public void FireFireball()
    {
        if (Target == null || !IsFireballReady || DevilData == null) return;

        fireballCooldown = DevilData.FireballCooldown;
        PlayAnim(MonsterAnimId.RangeAttack);
        FireProjectileAsync(Target, 1, 0f).Forget();
    }
    #endregion

    #region 스킬 — 비행
    /// <summary>타깃이 꼬리치기 사거리 안에 있는지.</summary>
    public bool IsTargetInTailRange()
        => Target != null && PlanarDistanceToTarget() <= TailRange;

    /// <summary>타깃이 급강하 사거리 안에 있는지.</summary>
    public bool IsTargetInDiveRange()
        => Target != null && PlanarDistanceToTarget() <= DiveRange;

    /// <summary>화염 연사. 지상 화염구와 같은 투사체를 여러 발 쏜다.</summary>
    public void FireFlyProjectileBurst()
    {
        if (Target == null || !IsFlyProjectileReady || DevilData == null) return;

        flyProjectileCooldown = DevilData.FlyProjectileCooldown;
        PlayAnim(MonsterAnimId.FlyRangeAttack);
        FireProjectileAsync(Target, Mathf.Max(DevilData.FlyProjectileBurst, 1), DevilData.FlyProjectileInterval).Forget();
    }

    /// <summary>꼬리치기. 사거리 안 타깃에게 피해를 주고 뒤로 밀어낸다.</summary>
    public void PerformTailAttack()
    {
        if (DevilData == null) return;

        tailCooldown = DevilData.TailCooldown;
        PlayAnim(MonsterAnimId.FlyTail);

        if (Target == null || !Target.IsAlive) return;
        if (!IsTargetInTailRange()) return;

        int damage = ResolveDamage(DevilData.TailDamage);
        ApplyDamageToTarget(Target, damage);

        // 데빌 → 플레이어 방향으로 밀어낸다.
        Vector3 push = Target.transform.position - transform.position;
        push.y = 0f;
        Target.Motor?.AddKnockback(push, DevilData.TailKnockback, DevilData.TailKnockbackDuration);
    }

    /// <summary>급강하 쿨타임을 시작한다. (DevilDiveState 진입 시 호출)</summary>
    public void StartDiveCooldown()
    {
        if (DevilData != null)
            diveCooldown = DevilData.DiveCooldown;
    }

    /// <summary>급강하 착지 지점 광역 피해.</summary>
    public void PerformDiveImpact(Vector3 impactPos)
    {
        if (DevilData == null) return;

        int damage = ResolveDamage(DevilData.DiveDamage);
        float radius = Mathf.Max(DevilData.DiveImpactRadius, 0f);
        if (damage <= 0 || radius <= 0f) return;

        var hits = Physics.OverlapSphere(impactPos, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive) continue;

            ApplyDamageToTarget(player, damage);
            return; // 같은 플레이어를 여러 콜라이더로 중복 타격하지 않는다
        }
    }

    /// <summary>
    /// 지옥 세례. 기존 Meteor 장판을 그대로 재사용하되 원의 중심을 **타깃 발밑**으로 잡는다.
    /// (지상 데몬의 운석 세례는 자기 주변이라 "가까이 오지 마" 견제였지만,
    ///  공중에서 타깃 중심으로 깔면 "계속 움직여라" 강제 이동기가 된다)
    /// </summary>
    public void CastHellRain()
    {
        if (Target == null || !IsHellRainReady || DevilData == null) return;

        hellRainCooldown = DevilData.HellRainCooldown;
        PlayAnim(MonsterAnimId.FlyCast);

        // 시전 시점의 타깃 위치를 원의 중심으로 고정한다(이후 플레이어가 움직여도 세례는 이 자리에 떨어진다).
        CastHellRainAsync(Target.transform.position).Forget();
    }

    private async UniTaskVoid CastHellRainAsync(Vector3 center)
    {
        var data = DevilData;
        int count = Mathf.Max(data.MeteorCount, 0);
        if (count == 0) return;

        int damage = ResolveDamage(data.MeteorDamage);

        // 각 운석을 겹치지 않는 각도 섹터에 하나씩 배치하고, 반지름은 √u 분포로 뽑아 원 전체에 고르게 퍼뜨린다.
        float sectorStep = 360f / count;
        float sectorJitter = sectorStep * 0.5f;
        float stagger = Mathf.Max(data.MeteorSpawnStagger, 0f);

        for (int i = 0; i < count; i++)
        {
            float angleDeg = i * sectorStep + Random.Range(-sectorJitter, sectorJitter);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float dist = data.MeteorRadius * Mathf.Sqrt(Random.value);

            Vector3 impact = center + new Vector3(Mathf.Cos(angleRad) * dist, 0f, Mathf.Sin(angleRad) * dist);
            impact.y = center.y; // 타깃 발치 높이를 지면으로 간주

            float warning = data.MeteorWarningTime + i * stagger;

            var meteor = await Extensions.SpawnAsync<Meteor>(data.MeteorKey);
            if (meteor == null) continue;

            meteor.Init(gameObject, impact, damage, data.MeteorImpactRadius, warning, data.MeteorLingerTime);
        }
    }
    #endregion

    #region 공용 헬퍼
    /// <summary>스킬 고유 데미지가 0 이하면 기본 공격력으로 대체한다.</summary>
    private int ResolveDamage(float skillDamage)
    {
        float raw = skillDamage > 0f
            ? skillDamage
            : (status != null ? status.Attack : (statData != null ? statData.AttackDamage : 0f));

        return Mathf.Max(Mathf.RoundToInt(raw * OutgoingDamageMultiplier), 0);
    }

    private void ApplyDamageToTarget(Player player, int damage)
    {
        var ctx = new DamageContext(
            instigator: gameObject,
            point: player.transform.position,
            amount: damage,
            toolId: string.Empty);

        if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
            dmg.ApplyDamage(ctx);
    }

    /// <summary>투사체를 count발, interval 간격으로 발사한다.</summary>
    private async UniTaskVoid FireProjectileAsync(Player target, int count, float interval)
    {
        var data = DevilData;
        int damage = ResolveDamage(data.ProjectileDamage);

        for (int i = 0; i < count; i++)
        {
            // 연사 도중 타깃이 죽거나 사라지면 중단
            if (target == null || !target.isActiveAndEnabled) return;

            var projectile = await Extensions.SpawnAsync<MonsterProjectile>(data.ProjectileKey);
            if (projectile == null) return;

            if (target == null || !target.isActiveAndEnabled)
            {
                Extensions.Despawn(projectile.gameObject);
                return;
            }

            Vector3 origin = transform.position + Vector3.up * data.MuzzleHeight;
            Vector3 aim = target.transform.position + Vector3.up * data.MuzzleHeight;
            Vector3 dir = aim - origin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward;

            projectile.Init(gameObject, origin, dir.normalized, data.ProjectileSpeed, damage, data.ProjectileLifeTime);

            if (i < count - 1 && interval > 0f)
                await UniTask.Delay(Mathf.RoundToInt(interval * 1000f), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }
    #endregion
}

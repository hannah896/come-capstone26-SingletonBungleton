using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>트리가드 기술 종류. 쿨타임 배열 인덱스로도 쓴다.</summary>
public enum TreeguardSkill
{
    Swing,      // 가지 휩쓸기
    Smack,      // 고목 내려찍기
    Kick,       // 밀쳐내기
    Step,       // 대지 울림
    Clap,       // 박수 충격파
    Projectile, // 가지 투척
    Summon,     // 숲의 부름
}

/// <summary>
/// 만월 보스 트리가드. (기획: docs/기획_트리가드_스킬로직.md)
///
/// "움직이는 성벽. 큰 기술을 읽고, 빈틈에 때려라."
/// 느리게 돌고, 단단하고, 한 방이 아프다. 근접 5종이 각자 다른 회피 방향을 요구하고,
/// 큰 기술 뒤에는 반드시 빈틈이 있다. Cast Spell은 트리앤트 미니언 소환 전용이다.
///
/// - 강인도: 평소에는 맞아도 움찔하지 않는다(슈퍼아머). 강인도가 0이 되면 그로기로 무방비가 된다.
/// - 페이즈: HP 60% / 30%에서 전환. 전환 연출 동안 무적이며 미니언을 소환한다. 3페이즈는 뛴다.
///
/// 기술 선택은 TreeguardAttackState가, 기술 1회 진행은 TreeguardSkillState가,
/// 판정/장판/소환/쿨타임/강인도/페이즈 메카닉은 이 본체가 담당한다.
/// </summary>
public class Treeguard : Monster
{
    #region Inspector
    [Header("트리가드 - 애니메이션 Bool 파라미터명 (Treant_Forest 컨트롤러 기준, 없으면 비워둠)")]
    [Tooltip("3페이즈 이동 (Run Forward In Place). 평소 이동은 베이스 moveBool(Walk)")]
    [SerializeField] private string runBool = "";
    [Tooltip("고목 내려찍기 (Smack Attack). 가지 휩쓸기(Swing)는 베이스 attackBool")]
    [SerializeField] private string smackBool = "";
    [Tooltip("밀쳐내기 (Kick Attack)")]
    [SerializeField] private string kickBool = "";
    [Tooltip("대지 울림 (Step Attack)")]
    [SerializeField] private string stepBool = "";
    [Tooltip("박수 충격파 (Clap Attack)")]
    [SerializeField] private string clapBool = "";
    [Tooltip("가지 투척 (Projectile Attack)")]
    [SerializeField] private string rangeAttackBool = "";
    [Tooltip("숲의 부름 (Cast Spell)")]
    [SerializeField] private string castSpellBool = "";
    #endregion

    private int runBoolHash;
    private int smackBoolHash;
    private int kickBoolHash;
    private int stepBoolHash;
    private int clapBoolHash;
    private int rangeAttackBoolHash;
    private int castSpellBoolHash;

    private static readonly int SkillCount = System.Enum.GetValues(typeof(TreeguardSkill)).Length;
    private readonly float[] cooldowns = new float[SkillCount];

    private int phase = 1;
    private bool phaseShiftPending;

    private float poise;
    private float poiseRegenWait;
    private bool isGroggy;
    private bool isInvulnerable;

    private readonly List<Monster> minions = new List<Monster>();
    private int pendingSummons; // 로드 중인 소환 미니언 수 (로드가 끝나기 전에 또 소환하지 않도록 함께 센다)
    private int minionKeyIndex;

    private TreeguardStatData BossData => statData as TreeguardStatData;

    /// <summary>스탯 SO. 상태 클래스가 거리·조건 값을 읽는다. 주입 전이면 null.</summary>
    public TreeguardStatData Data => BossData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>현재 페이즈 (1~3).</summary>
    public int Phase => phase;
    /// <summary>HP가 다음 페이즈 기준선 아래로 내려가 전환 연출을 기다리는 중인지.</summary>
    public bool IsPhaseShiftPending => phaseShiftPending;
    public float PhaseShiftDuration => BossData != null ? BossData.PhaseShiftDuration : 2f;
    /// <summary>기술과 기술 사이 최소 간격. 3페이즈는 더 짧다.</summary>
    public float SkillGap => BossData == null ? 0.8f : (phase >= 3 ? BossData.Phase3SkillGap : BossData.SkillGap);

    public float GroggyDuration => BossData != null ? BossData.GroggyDuration : 3f;
    public float GroggyClipLength => BossData != null ? BossData.GroggyClipLength : 0.83f;

    /// <summary>살아있는(로드 중 포함) 소환 미니언 수.</summary>
    public int AliveMinionCount
    {
        get
        {
            PruneMinions();
            return minions.Count + pendingSummons;
        }
    }

    /// <summary>타깃과의 수평 거리. 타깃이 없으면 무한대.</summary>
    public float TargetDistance => Target != null ? PlanarDistanceToTarget() : float.MaxValue;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        runBoolHash = ToAnimHash(runBool);
        smackBoolHash = ToAnimHash(smackBool);
        kickBoolHash = ToAnimHash(kickBool);
        stepBoolHash = ToAnimHash(stepBool);
        clapBoolHash = ToAnimHash(clapBool);
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        castSpellBoolHash = ToAnimHash(castSpellBool);
    }

    // 풀에서 재사용될 때 쿨타임/페이즈/강인도/소환 목록을 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        for (int i = 0; i < cooldowns.Length; i++)
            cooldowns[i] = 0f;

        phase = 1;
        phaseShiftPending = false;
        poise = BossData != null ? BossData.PoiseMax : 100f;
        poiseRegenWait = 0f;
        isGroggy = false;
        isInvulnerable = false;
        minions.Clear();
        pendingSummons = 0;
        minionKeyIndex = 0;
    }

    // 스탯 SO가 주입되면 강인도 최대치도 새 값으로 채운다.
    public override void ApplyStatData(EntityStatData data)
    {
        base.ApplyStatData(data);
        poise = BossData != null ? BossData.PoiseMax : 100f;
    }

    // 피격 경직 대신 강인도를 깎는다.
    protected override void OnStatusInitialized()
    {
        base.OnStatusInitialized();
        if (status != null)
            status.OnDamaged += HandlePoiseDamage;
    }

    protected override MonsterStateMachine CreateStateMachine()
        => new TreeguardStateMachine(this);

    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.Run         => runBoolHash,
        MonsterAnimId.Smack       => smackBoolHash,
        MonsterAnimId.Kick        => kickBoolHash,
        MonsterAnimId.Step        => stepBoolHash,
        MonsterAnimId.Clap        => clapBoolHash,
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        MonsterAnimId.CastSpell   => castSpellBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    protected override void OnGameUpdate(float deltaTime)
    {
        if (IsSimulatedPeer)
        {
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] > 0f)
                    cooldowns[i] -= deltaTime;
            }

            PoiseRegenStep(deltaTime);
            CheckPhase();
        }

        base.OnGameUpdate(deltaTime);
    }

    // 사망 시 소환한 미니언도 함께 시든다.
    protected override void OnDeath()
    {
        base.OnDeath();
        KillMinions();
    }

    #region 데미지 / 강인도 / 그로기
    // 페이즈 전환 연출 중에는 무적.
    public override bool CanDamage(DamageContext context)
        => !isInvulnerable && base.CanDamage(context);

    // 그로기 중에는 받는 데미지가 늘어난다.
    public override void ApplyDamage(DamageContext context)
    {
        if (isGroggy && BossData != null)
        {
            int amount = Mathf.RoundToInt(context.Amount * BossData.GroggyDamageMultiplier);
            context = new DamageContext(context.Instigator, context.Point, amount, context.ToolId,
                context.ActionType, context.HarvestableNodeTypes);
        }

        base.ApplyDamage(context);
    }

    private void HandlePoiseDamage(float amount)
    {
        // 강인도와 그로기 판단은 AI를 돌리는 피어만 한다.
        if (!IsSimulatedPeer || BossData == null) return;
        if (isGroggy || isInvulnerable) return;
        if (status == null || status.IsDead) return;

        poise -= amount;
        poiseRegenWait = BossData.PoiseRegenDelay;

        if (poise <= 0f)
        {
            poise = 0f;
            (stateMachine as TreeguardStateMachine)?.ToGroggy();
        }
    }

    private void PoiseRegenStep(float deltaTime)
    {
        if (BossData == null || isGroggy) return;

        if (poiseRegenWait > 0f)
        {
            poiseRegenWait -= deltaTime;
            return;
        }

        poise = Mathf.Min(poise + BossData.PoiseRegenPerSecond * deltaTime, BossData.PoiseMax);
    }

    /// <summary>그로기 상태를 켜고 끈다. 끝날 때 강인도를 가득 채운다. (TreeguardGroggyState가 호출)</summary>
    public void SetGroggy(bool value)
    {
        if (isGroggy == value) return;
        isGroggy = value;

        if (!value && BossData != null)
        {
            poise = BossData.PoiseMax;
            poiseRegenWait = 0f;
        }
    }

    /// <summary>무적을 켜고 끈다. (TreeguardPhaseShiftState가 호출)</summary>
    public void SetInvulnerable(bool value) => isInvulnerable = value;
    #endregion

    #region 페이즈
    private void CheckPhase()
    {
        if (BossData == null || status == null || status.IsDead || status.MaxHp <= 0f) return;
        if (phaseShiftPending) return;

        float ratio = status.CurrentHp / status.MaxHp;
        int target = ratio <= BossData.Phase3HpRatio ? 3 : (ratio <= BossData.Phase2HpRatio ? 2 : 1);
        if (target <= phase) return;

        // 한 번에 두 페이즈를 건너뛰어도 전환 연출은 한 번만 하고 바로 최종 페이즈로 간다.
        phase = target;
        phaseShiftPending = true;
    }

    /// <summary>전환 연출을 시작하며 대기 플래그를 끈다.</summary>
    public void ConsumePhaseShift() => phaseShiftPending = false;
    #endregion

    #region 쿨타임 / 판정 보조
    public bool IsSkillReady(TreeguardSkill skill) => cooldowns[(int)skill] <= 0f;

    private void StartCooldown(TreeguardSkill skill)
    {
        var d = BossData;
        if (d == null) return;

        float cd = skill switch
        {
            TreeguardSkill.Swing      => d.SwingCooldown,
            TreeguardSkill.Smack      => d.SmackCooldown,
            TreeguardSkill.Kick       => d.KickCooldown,
            TreeguardSkill.Step       => d.StepCooldown,
            TreeguardSkill.Clap       => d.ClapCooldown,
            TreeguardSkill.Projectile => d.ProjectileCooldown,
            TreeguardSkill.Summon     => phase >= 3 ? d.Phase3SummonCooldown : d.SummonCooldown,
            _ => 0f,
        };

        // 소환은 3페이즈 전용 쿨이 따로 있으므로 배율을 겹쳐 걸지 않는다.
        if (phase >= 3 && skill != TreeguardSkill.Summon)
            cd *= d.Phase3CooldownMultiplier;

        cooldowns[(int)skill] = cd;
    }

    /// <summary>타깃이 몸 정면 부채꼴(StepFrontAngle) 밖, 즉 옆이나 등 뒤에 있는지.</summary>
    public bool IsTargetBehind()
    {
        if (Target == null || BossData == null) return false;
        Vector3 d = Target.transform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return false;
        return Vector3.Angle(transform.forward, d) > BossData.StepFrontAngle * 0.5f;
    }

    /// <summary>
    /// 타깃 쪽으로 천천히 돌면서 몸 정면으로 전진한다. 회전이 느려서 크게 돌아 들어오는 궤적이 된다.
    /// 1~2페이즈는 걷고(Move), 3페이즈는 뛴다(Run).
    /// </summary>
    public void MoveStep(float deltaTime)
    {
        if (Target == null || status == null) return;

        bool running = phase >= 3;
        float speed = status.MoveSpeed;
        if (BossData != null)
            speed = running ? BossData.Phase3RunSpeed : (phase >= 2 ? BossData.Phase2MoveSpeed : status.MoveSpeed);

        PlayAnim(running ? MonsterAnimId.Run : MonsterAnimId.Move);
        FaceTargetStep(deltaTime);
        transform.position += transform.forward * (speed * deltaTime);
    }
    #endregion

    #region 기술 진행 (TreeguardSkillState가 호출)
    /// <summary>기술 클립에 대응하는 애니 ID.</summary>
    public static MonsterAnimId SkillAnim(TreeguardSkill skill) => skill switch
    {
        TreeguardSkill.Swing      => MonsterAnimId.Attack,
        TreeguardSkill.Smack      => MonsterAnimId.Smack,
        TreeguardSkill.Kick       => MonsterAnimId.Kick,
        TreeguardSkill.Step       => MonsterAnimId.Step,
        TreeguardSkill.Clap       => MonsterAnimId.Clap,
        TreeguardSkill.Projectile => MonsterAnimId.RangeAttack,
        TreeguardSkill.Summon     => MonsterAnimId.CastSpell,
        _ => MonsterAnimId.Idle,
    };

    /// <summary>기술 시작부터 타격(또는 소환)까지의 시간.</summary>
    public float SkillHitTime(TreeguardSkill skill)
    {
        var d = BossData;
        if (d == null) return 0.5f;
        return skill switch
        {
            TreeguardSkill.Swing      => d.SwingHitTime,
            TreeguardSkill.Smack      => d.SmackHitTime,
            TreeguardSkill.Kick       => d.KickHitTime,
            TreeguardSkill.Step       => d.StepHitTime,
            TreeguardSkill.Clap       => d.ClapHitTime,
            TreeguardSkill.Projectile => d.ProjectileWarningTime,
            TreeguardSkill.Summon     => d.SummonHitTime,
            _ => 0.5f,
        };
    }

    /// <summary>기술 시작부터 다음 판단까지의 전체 시간(클립 + 빈틈).</summary>
    public float SkillDuration(TreeguardSkill skill, bool combo = false)
    {
        var d = BossData;
        if (d == null) return 1.5f;
        if (combo && skill == TreeguardSkill.Swing) return d.ComboSwingDuration;
        return skill switch
        {
            TreeguardSkill.Swing      => d.SwingDuration,
            TreeguardSkill.Smack      => d.SmackDuration,
            TreeguardSkill.Kick       => d.KickDuration,
            TreeguardSkill.Step       => d.StepDuration,
            TreeguardSkill.Clap       => d.ClapDuration,
            TreeguardSkill.Projectile => d.ProjectileDuration,
            TreeguardSkill.Summon     => d.SummonDuration,
            _ => 1.5f,
        };
    }

    /// <summary>
    /// 기술 클립을 붙잡아 두는 시간. 이 시간이 지나면 Idle로 돌아가 빈틈 동안 서 있는다.
    /// 내려찍기만은 빈틈 내내 클립 마지막 프레임(팔이 땅에 박힌 자세)을 유지한다.
    /// </summary>
    public float SkillAnimHoldTime(TreeguardSkill skill, bool combo = false)
    {
        if (skill == TreeguardSkill.Smack) return SkillDuration(skill, combo);
        return BossData != null ? BossData.MeleeClipLength : 1.17f;
    }

    /// <summary>3페이즈에서 내려찍기 직후 휩쓸기로 이어 갈지 굴린다.</summary>
    public bool RollPhase3Combo()
        => phase >= 3 && BossData != null && Random.value < BossData.Phase3ComboChance;

    /// <summary>
    /// 기술을 시작한다: 쿨타임을 걸고 클립을 재생하며, 장판 기술은 이 순간 예고 장판을 깐다.
    /// 장판의 예고 시간이 곧 타격 시점이라 클립과 장판 폭발이 맞물린다.
    /// </summary>
    public void BeginSkill(TreeguardSkill skill)
    {
        StartCooldown(skill);
        PlayAnim(SkillAnim(skill));

        var d = BossData;
        if (d == null) return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

        switch (skill)
        {
            case TreeguardSkill.Smack:
                SpawnZone(origin + forward * d.SmackForwardOffset, d.SmackRadius, d.SmackHitTime, d.SmackDamage);
                break;

            case TreeguardSkill.Step:
                SpawnZone(origin, d.StepRadius, d.StepHitTime, d.StepDamage, d.StepSlowMultiplier, d.StepSlowDuration);
                break;

            case TreeguardSkill.Clap:
                // 몸 앞에서부터 직선으로 하나씩 이어 터진다. 옆으로 한 걸음이면 피한다.
                for (int i = 0; i < d.ClapWaveCount; i++)
                {
                    Vector3 pos = origin + forward * (d.ClapWaveSpacing * (i + 1));
                    SpawnZone(pos, d.ClapWaveRadius, d.ClapHitTime + d.ClapWaveInterval * i, d.ClapDamage);
                }
                break;

            case TreeguardSkill.Projectile:
                // 투사체 물리 없이 타깃 발밑에 낙하 장판을 깐다. 예고를 보고 그 자리를 벗어나면 피한다.
                if (Target != null)
                    SpawnZone(Target.transform.position, d.ProjectileRadius, d.ProjectileWarningTime, d.ProjectileDamage);
                break;
        }
    }

    /// <summary>타격 시점 처리. 부채꼴 근접기는 판정을, 숲의 부름은 소환을 한다. (장판 기술은 장판이 알아서 터진다)</summary>
    public void SkillHit(TreeguardSkill skill)
    {
        var d = BossData;
        if (d == null) return;

        switch (skill)
        {
            case TreeguardSkill.Swing:
                FanHit(d.SwingRange, d.SwingAngle, d.SwingDamage, d.SwingKnockback, 0.35f);
                break;
            case TreeguardSkill.Kick:
                FanHit(d.KickRange, d.KickAngle, d.KickDamage, d.KickKnockback, 0.4f);
                break;
            case TreeguardSkill.Summon:
                SummonMinions();
                break;
        }
    }
    #endregion

    #region 판정 / 장판
    /// <summary>전방 부채꼴 안의 모든 플레이어에게 데미지와 넉백을 준다. (TreantTree.PunchHit과 같은 방식)</summary>
    private void FanHit(float range, float angle, float rawDamage, float knockback, float knockbackDuration)
    {
        int damage = Mathf.Max(Mathf.RoundToInt(rawDamage > 0f ? rawDamage : (status != null ? status.Attack : 0f)), 0);
        float halfAngle = angle * 0.5f;

        var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, range,
            ~0, QueryTriggerInteraction.Collide);

        // 한 플레이어가 콜라이더를 여러 개 가질 수 있으므로 중복 타격을 막는다.
        var struck = new HashSet<Player>();

        foreach (var col in hits)
        {
            var player = col.GetComponentInParent<Player>();
            if (player == null || !player.IsAlive) continue;
            if (!struck.Add(player)) continue;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, toPlayer) > halfAngle)
                continue;

            var ctx = new DamageContext(
                instigator: gameObject,
                point: player.transform.position,
                amount: damage,
                toolId: string.Empty);

            if (player.TryGetComponent<IDamageable>(out var dmg) && dmg.CanDamage(ctx))
                dmg.ApplyDamage(ctx);

            if (knockback > 0f)
            {
                Vector3 push = toPlayer.sqrMagnitude < 0.0001f ? transform.forward : toPlayer.normalized;
                player.Motor?.AddKnockback(push, knockback, knockbackDuration);
            }
        }
    }

    /// <summary>예고 장판을 깐다. 범용 장판(Meteor)이 예고·판정·둔화·네트워크 연출 복제를 처리한다.</summary>
    private void SpawnZone(Vector3 center, float radius, float warningTime, float rawDamage,
                           float slowMultiplier = 1f, float slowDuration = 0f)
        => SpawnZoneAsync(center, radius, warningTime, rawDamage, slowMultiplier, slowDuration).Forget();

    private async UniTaskVoid SpawnZoneAsync(Vector3 center, float radius, float warningTime, float rawDamage,
                                             float slowMultiplier, float slowDuration)
    {
        var d = BossData;
        var zone = await Extensions.SpawnAsync<Meteor>(d.ZoneKey);
        if (zone == null) return;

        int damage = Mathf.Max(Mathf.RoundToInt(rawDamage), 0);
        zone.Init(gameObject, center, damage, radius, warningTime, d.ZoneLingerTime,
            dotDamage: 0f, dotDuration: 0f, dotInterval: 1f,
            slowMultiplier: slowMultiplier, slowDuration: slowDuration);
    }
    #endregion

    #region 숲의 부름 (미니언 소환)
    private void PruneMinions()
        => minions.RemoveAll(m => m == null || !m.isActiveAndEnabled || m.Status == null || m.Status.IsDead);

    /// <summary>
    /// 보스 주변 원 위에 트리앤트 미니언을 소환한다. 마릿수는 페이즈별 값이고, 살아있는 소환 미니언 상한을 넘지 않는다.
    /// 소환 미니언은 MonsterCatalog의 스탯 SO를 쓰고, 멀티에서는 복제 대상으로 등록한다.
    /// </summary>
    public void SummonMinions()
    {
        var d = BossData;
        if (d == null || d.MinionKeys == null || d.MinionKeys.Length == 0) return;

        // 페이즈 전환 소환도 쿨을 건다. 안 걸면 전환 직후 공격 상태가 곧바로 한 번 더 부른다.
        StartCooldown(TreeguardSkill.Summon);
        PruneMinions();
        int perPhase = d.SummonCountPerPhase != null && d.SummonCountPerPhase.Length > 0
            ? d.SummonCountPerPhase[Mathf.Clamp(phase - 1, 0, d.SummonCountPerPhase.Length - 1)]
            : 2;
        int count = Mathf.Min(perPhase, d.MaxAliveMinions - minions.Count - pendingSummons);

        // 원 위에 균등 배치하되, 매번 같은 자리에 나오지 않도록 시작 각도를 섞는다.
        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + 360f * i / Mathf.Max(count, 1);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * d.SummonRadius;
            string key = d.MinionKeys[minionKeyIndex++ % d.MinionKeys.Length];
            pendingSummons++;
            SummonMinionAsync(key, transform.position + offset).Forget();
        }
    }

    private async UniTaskVoid SummonMinionAsync(string key, Vector3 position)
    {
        try
        {
            await SummonMinionCoreAsync(key, position);
        }
        finally
        {
            pendingSummons = Mathf.Max(pendingSummons - 1, 0);
        }
    }

    private async UniTask SummonMinionCoreAsync(string key, Vector3 position)
    {
        var catalog = MonsterCatalog.Instance;
        byte catalogId = catalog != null ? catalog.GetCatalogId(key) : (byte)0;
        if (catalogId == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[Treeguard] 소환 미니언 '{key}'가 MonsterCatalog에 없습니다.", this);
#endif
            return;
        }

        var entry = catalog.GetEntry(catalogId);
        var minion = await Monster.SpawnAsync(entry.addressableKey, entry.statData, position);
        if (minion == null) return;

        // 로드 대기 중에 보스가 죽었으면 바로 정리한다.
        if (status == null || status.IsDead || !isActiveAndEnabled)
        {
            Extensions.Despawn(minion.gameObject);
            return;
        }

        minions.Add(minion);

#if PHOTON_FUSION
        // 멀티플레이면 복제 대상으로 등록한다. 싱글이면 디렉터가 없어 로컬 몬스터로 남는다.
        if (Main.Network != null && Main.Network.IsInRoom)
            NetworkMonsterDirector.Instance?.RegisterMonster(minion, catalogId);
#endif
    }

    /// <summary>살아있는 소환 미니언을 모두 사망 처리한다(보스 사망 시).</summary>
    private void KillMinions()
    {
        PruneMinions();
        foreach (var minion in minions)
        {
            var s = minion.Status;
            if (s == null || s.IsDead) continue;
            // 방어력을 넘는 피해로 확실히 죽인다. 사망 흐름(연출 → 디스폰)은 미니언이 그대로 처리한다.
            s.TakeDamage(s.CurrentHp + s.Defense + 1f);
        }
        minions.Clear();
    }
    #endregion
}

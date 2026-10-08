using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 만월 보스: 외눈 박쥐 메이지 (Cyclops Bat Mage).
/// - 슬래시: 정면 단일 타깃 근접 공격
/// - 스핀: 제자리에서 회전하며 주변 플레이어 전원을 여러 번 친다. 마지막 타격에서 바깥으로 밀어낸다
/// - 스펠: 발밑 바닥에 별(펜타그램) 모양 마법 레이저를 깐다. 예고선 밖으로 피해야 한다
///   2페이즈(체력 Phase2HpRatio 이하)에서는 36° 엇갈린 별이 한 번 더 깔린다
/// - 회복: 반경 안에 체력이 HealTriggerHpRatio 이하인 아군 몬스터가 있으면, 다친 아군 전원을 최대 체력의 일정 비율만큼 회복
///   (기본은 자기 자신 제외 — 메이지부터 잡아야 한다는 압박을 주는 역할)
/// 이동: 배회는 Walk(Move), 추적은 Chase Bool을 쓴다 (베이스 Monster의 chaseBool).
/// 보스라 넉백을 받지 않고, 피격 경직은 StaggerCooldown 간격으로만 걸린다.
/// 기술 선택/실행은 BatMageAttackState가, 판정·소환·쿨타임은 이 본체가 담당한다.
/// </summary>
public class BatMage : Monster
{
    #region Inspector
    [Header("메이지 - 애니메이션 Bool 파라미터명 (CyClopBat_Mage 컨트롤러 기준, 없으면 비워둠)")]
    [SerializeField] private string spinBool = "SpinAttack";
    [SerializeField] private string spellBool = "Cast";
    [SerializeField] private string healBool = "Heal";

    [Header("메이지 - 애니메이터 상태 이름 (판정 타이밍을 클립 시작에 맞추는 데 사용)")]
    [SerializeField] private string slashStateName = "Slash Attack";
    [SerializeField] private string spinStateName = "Spin Attack";
    [SerializeField] private string spellStateName = "Cast Spell";
    [SerializeField] private string healStateName = "Summon Attack";

    [Header("메이지 - 효과음 (3D)")]
    [SerializeField] private AudioClip swingSfx;
    #endregion

    private int spinBoolHash;
    private int spellBoolHash;
    private int healBoolHash;
    private int healStateHash;
    private int slashStateHash;
    private int spinStateHash;
    private int spellStateHash;

    private float slashCooldown;
    private float spinCooldown;
    private float spellCooldown;
    private float healCooldown;
    private float staggerCooldown;
    private float secondStarTimer = -1f; // 2페이즈 두 번째 별까지 남은 시간 (<0이면 예약 없음)
    private float secondStarYaw;

    private AudioSource sfxSource;
    private readonly HashSet<Player> struck = new HashSet<Player>();
    private readonly HashSet<Monster> healTargets = new HashSet<Monster>();

    private BatMageStatData MageData => statData as BatMageStatData;

    #region Properties (상태 클래스에서 사용)
    public float SlashRange => MageData != null ? MageData.SlashRange : 2.8f;
    public float SlashHitTime => MageData != null ? MageData.SlashHitTime : 0.35f;
    public float SlashDuration => MageData != null ? MageData.SlashDuration : 0.8f;
    public float SpinRadius => MageData != null ? MageData.SpinRadius : 3.5f;
    public float SpinDuration => MageData != null ? MageData.SpinDuration : 1.8f;
    public float[] SpinHitTimes => MageData != null && MageData.SpinHitTimes != null && MageData.SpinHitTimes.Length > 0
        ? MageData.SpinHitTimes : new[] { 0.4f, 1.0f, 1.6f };
    public float SpinMoveSpeed => status != null ? status.MoveSpeed * (MageData != null ? MageData.SpinMoveMultiplier : 0.5f) : 0f;
    public float SpellCastPoint => MageData != null ? MageData.SpellCastPoint : 0.5f;
    public float SpellDuration => MageData != null ? MageData.SpellDuration : 1.0f;
    public float SkillGap => MageData != null ? MageData.SkillGap : 0.6f;
    public float HealRadius => MageData != null ? MageData.HealRadius : 10f;
    public float HealCastPoint => MageData != null ? MageData.HealCastPoint : 0.5f;
    public float HealDuration => MageData != null ? MageData.HealDuration : 1.0f;

    public bool IsSlashReady => slashCooldown <= 0f;
    public bool IsSpinReady => spinCooldown <= 0f;
    public bool IsSpellReady => spellCooldown <= 0f;
    public bool IsHealReady => healCooldown <= 0f;
    public bool IsPhase2 => HpRatio <= (MageData != null ? MageData.Phase2HpRatio : 0.5f);
    #endregion

    // 보스: 칼 한 방에 밀리지 않는다
    protected override bool ResistsKnockback => true;

    protected override void Awake()
    {
        base.Awake();
        spinBoolHash = ToAnimHash(spinBool);
        spellBoolHash = ToAnimHash(spellBool);
        slashStateHash = ToAnimHash(slashStateName);
        spinStateHash = ToAnimHash(spinStateName);
        spellStateHash = ToAnimHash(spellStateName);
        healBoolHash = ToAnimHash(healBool);
        healStateHash = ToAnimHash(healStateName);

        sfxSource = gameObject.GetOrAddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 1f;
        sfxSource.minDistance = 5f;
        sfxSource.maxDistance = 35f;
        sfxSource.rolloffMode = AudioRolloffMode.Linear;
    }

    // 풀에서 재사용될 때 쿨타임/예약을 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        slashCooldown = 0f;
        spinCooldown = 0f;
        spellCooldown = 0f;
        healCooldown = 0f;
        staggerCooldown = 0f;
        secondStarTimer = -1f;
    }

    protected override MonsterStateMachine CreateStateMachine()
        => new BatMageStateMachine(this);

    // 메이지 전용 애니(스핀/스펠)를 매핑에 더한다. 슬래시는 베이스 attackBool(Attack)을 쓴다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.Spin      => spinBoolHash,
        MonsterAnimId.CastSpell => spellBoolHash,
        MonsterAnimId.Heal      => healBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 휘두르는 소리는 클라에서도 나야 하므로 애니 변경 훅에서 재생한다.
    protected override void OnAnimChanged(MonsterAnimId animId)
    {
        if (animId == MonsterAnimId.Attack || animId == MonsterAnimId.Spin)
            PlaySfx(swingSfx);
    }

    // 쿨타임과 두 번째 별 예약은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (slashCooldown > 0f) slashCooldown -= deltaTime;
        if (spinCooldown > 0f) spinCooldown -= deltaTime;
        if (spellCooldown > 0f) spellCooldown -= deltaTime;
        if (healCooldown > 0f) healCooldown -= deltaTime;
        if (staggerCooldown > 0f) staggerCooldown -= deltaTime;

        if (secondStarTimer >= 0f && IsSimulatedPeer)
        {
            secondStarTimer -= deltaTime;
            if (secondStarTimer < 0f)
                SpawnStarAsync(secondStarYaw).Forget();
        }

        base.OnGameUpdate(deltaTime);
    }

    #region Animator
    /// <summary>
    /// 애니메이터가 실제로 해당 기술 클립에 들어갔는지. Bool → Exit → Entry 경로라 클립 시작까지 지연이 있어서
    /// 판정 시간은 이 값이 true가 된 순간부터 센다. 애니메이터가 없거나 이름이 비어 있으면 바로 true.
    /// </summary>
    public bool IsInSkillClip(MonsterAnimId skill)
    {
        if (Animator == null) return true;
        int hash = skill switch
        {
            MonsterAnimId.Attack    => slashStateHash,
            MonsterAnimId.Spin      => spinStateHash,
            MonsterAnimId.CastSpell => spellStateHash,
            MonsterAnimId.Heal      => healStateHash,
            _ => 0,
        };
        if (hash == 0) return true;
        return Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
    }
    #endregion

    #region Stagger
    /// <summary>경직 가능한지 확인하고, 가능하면 쿨타임을 건다. (보스라 매 타격마다 끊기지 않게)</summary>
    public bool TryConsumeStagger()
    {
        if (staggerCooldown > 0f) return false;
        staggerCooldown = MageData != null ? MageData.StaggerCooldown : 5f;
        return true;
    }
    #endregion

    #region Skills
    public bool IsTargetInSlashRange()
        => Target != null && PlanarDistanceToTarget() <= SlashRange;

    public bool IsTargetInSpinRadius()
        => Target != null && PlanarDistanceToTarget() <= SpinRadius;

    /// <summary>스핀 범위 안에 있는 살아있는 플레이어 수.</summary>
    public int CountPlayersInSpin()
    {
        struck.Clear();
        foreach (var col in Physics.OverlapSphere(transform.position, SpinRadius, ~0, QueryTriggerInteraction.Collide))
        {
            var player = col.GetComponentInParent<Player>();
            if (player != null && player.IsAlive && IsWithinPlanar(player.transform.position, SpinRadius))
                struck.Add(player);
        }
        return struck.Count;
    }

    public void StartSlashCooldown() => slashCooldown = MageData != null ? MageData.SlashCooldown : 1.6f;
    public void StartSpinCooldown() => spinCooldown = MageData != null ? MageData.SpinCooldown : 8f;
    public void StartSpellCooldown() => spellCooldown = MageData != null ? MageData.SpellCooldown : 10f;
    public void StartHealCooldown() => healCooldown = MageData != null ? MageData.HealCooldown : 15f;

    /// <summary>
    /// 회복 반경 안의 살아있는 아군 몬스터를 healTargets에 모은다(체력이 가득 찬 몬스터 제외).
    /// 그중 HealTriggerHpRatio 이하인 몬스터가 하나라도 있으면 true — 회복을 시전할 가치가 있다.
    /// </summary>
    public bool CollectHealTargets()
    {
        healTargets.Clear();
        float trigger = MageData != null ? MageData.HealTriggerHpRatio : 0.6f;
        bool includeSelf = MageData != null && MageData.HealSelf;
        bool worth = false;

        foreach (var col in Physics.OverlapSphere(transform.position, HealRadius, ~0, QueryTriggerInteraction.Collide))
        {
            var monster = col.GetComponentInParent<Monster>();
            if (monster == null || !monster.isActiveAndEnabled) continue;
            if (monster == this && !includeSelf) continue;
            if (monster.Status == null || monster.Status.IsDead) continue;
            if (!IsWithinPlanar(monster.transform.position, HealRadius)) continue;

            float ratio = monster.HpRatio;
            if (ratio >= 0.999f) continue;
            healTargets.Add(monster);
            if (ratio <= trigger) worth = true;
        }
        return worth;
    }

    /// <summary>
    /// 회복 시전: 반경 안의 다친 아군 전원을 각자 최대 체력의 HealAmountRatio만큼 회복하고 초록 원 연출을 띄운다.
    /// 시전 순간에 대상을 다시 모은다(모션 동안 죽거나 반경을 벗어난 몬스터 제외).
    /// </summary>
    public void CastHeal()
    {
        CollectHealTargets();
        float ratio = MageData != null ? MageData.HealAmountRatio : 0.3f;
        foreach (var monster in healTargets)
            monster.ReceiveHeal(monster.Status.MaxHp * ratio);

        SpawnHealCircleAsync().Forget();
    }

    private async UniTaskVoid SpawnHealCircleAsync()
    {
        var data = MageData;
        if (data == null || string.IsNullOrEmpty(data.HealCircleKey)) return;
        Vector3 center = transform.position;
        var circle = await Extensions.SpawnAsync<MageHealCircle>(data.HealCircleKey);
        if (circle == null) return;
        circle.Play(center, data.HealRadius);
    }

    /// <summary>슬래시 판정: 현재 타깃 하나만. 거리(1.2배)·정면 각도 밖으로 피했으면 빗나간다.</summary>
    public void SlashHit()
    {
        if (Target == null || !Target.IsAlive) return;
        if (PlanarDistanceToTarget() > SlashRange * 1.2f) return;

        Vector3 to = Target.transform.position - transform.position;
        to.y = 0f;
        float half = (MageData != null ? MageData.SlashAngle : 100f) * 0.5f;
        if (to.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, to) > half) return;

        DealDamage(Target, MageData != null ? MageData.SlashDamage : 20f);
    }

    /// <summary>스핀 판정: 범위 안 플레이어 전원에게 1회씩. 마지막 타격이면 바깥으로 밀어낸다.</summary>
    public void SpinHit(bool last)
    {
        float dmg = MageData != null ? MageData.SpinDamage : 12f;
        CountPlayersInSpin(); // struck 갱신 (중복 콜라이더 제거)

        foreach (var player in struck)
        {
            DealDamage(player, dmg);

            if (last && MageData != null && MageData.SpinKnockback > 0f)
            {
                Vector3 push = player.transform.position - transform.position;
                player.Motor?.AddKnockback(push, MageData.SpinKnockback, MageData.SpinKnockbackDuration);
            }
        }
    }

    /// <summary>
    /// 스펠: 발밑에 별 레이저를 깐다. 첫 꼭짓점은 보는 방향.
    /// 2페이즈면 36° 엇갈린 두 번째 별을 Phase2SecondStarDelay 뒤에 예약한다.
    /// </summary>
    public void CastStars()
    {
        float yaw = transform.eulerAngles.y;
        SpawnStarAsync(yaw).Forget();

        if (IsPhase2 && MageData != null)
        {
            secondStarYaw = yaw + 36f;
            secondStarTimer = MageData.Phase2SecondStarDelay;
        }
    }

    private async UniTaskVoid SpawnStarAsync(float yaw)
    {
        var data = MageData;
        if (data == null || string.IsNullOrEmpty(data.StarLaserKey)) return;

        Vector3 center = transform.position;
        var laser = await Extensions.SpawnAsync<MageStarLaser>(data.StarLaserKey);
        if (laser == null) return;

        int damage = Mathf.Max(Mathf.RoundToInt(data.StarDamage), 0);
        laser.Init(gameObject, center, yaw, data.StarRadius, data.StarLaserWidth, damage,
                   data.StarWarningTime, data.StarLaserTime);
    }

    /// <summary>회전하며 타깃 쪽으로 천천히 다가간다(스핀 중).</summary>
    public void SpinMoveStep(float deltaTime)
    {
        if (Target == null) return;
        Vector3 dir = Target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.25f) return; // 거의 겹쳤으면 멈춤
        transform.position += dir.normalized * SpinMoveSpeed * deltaTime;
    }
    #endregion

    #region Helpers
    private bool IsWithinPlanar(Vector3 worldPos, float range)
    {
        Vector3 d = worldPos - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= range * range;
    }

    private void DealDamage(Player player, float amount)
    {
        int dmg = Mathf.Max(Mathf.RoundToInt(amount), 0);
        var ctx = new DamageContext(
            instigator: gameObject,
            point: player.transform.position,
            amount: dmg,
            toolId: string.Empty);

        if (player.TryGetComponent<IDamageable>(out var d) && d.CanDamage(ctx))
            d.ApplyDamage(ctx);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.volume = Extensions.GetSFXVolume();
        sfxSource.PlayOneShot(clip);
    }
    #endregion
}

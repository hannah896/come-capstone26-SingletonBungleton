using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 외눈 박쥐 위저드 (Cyclops Bat Wizard). 정예급 원거리 마법사 — 만월 보스는 아니다.
/// - 마법탄: 떨어져 있으면 쏘는 주력기
/// - 스펠(추적 운석): 타깃 발밑에 운석 장판을 MeteorInterval 간격으로 MeteorCount개 떨어뜨린다. 매번 그 순간의 타깃 위치를 노리고,
///   맞으면 잠깐 느려진다. 계속 움직이게 만드는 기술
/// - 근접: 가까우면 Slash / Slice를 번갈아 친다(단일 타깃)
/// - 스핀: 바짝 붙으면 짧게 회전하며 주변 전원을 친다. 마지막 타격에서 밀어낸다
/// 이동: 배회는 Walk(Move), 추적은 Chase Bool (메이지와 같은 구조).
/// 기술 선택/실행은 BatWizardAttackState가, 판정·소환·쿨타임은 이 본체가 담당한다.
/// </summary>
public class BatWizard : Monster
{
    #region Inspector
    [Header("위저드 - 애니메이션 Bool 파라미터명 (CyClopBat_Wizard 컨트롤러 기준, 없으면 비워둠)")]
    [SerializeField] private string sliceBool = "SliceAttack";
    [SerializeField] private string projectileBool = "ProjectileAttack";
    [SerializeField] private string spinBool = "SpinAttack";
    [SerializeField] private string spellBool = "Cast";

    [Header("위저드 - 애니메이터 상태 이름 (판정 타이밍을 클립 시작에 맞추는 데 사용)")]
    [SerializeField] private string slashStateName = "Slash Attack";
    [SerializeField] private string sliceStateName = "Slice Attack";
    [SerializeField] private string projectileStateName = "Projectile Attack";
    [SerializeField] private string spinStateName = "Spin Attack";
    [SerializeField] private string spellStateName = "Cast Spell";

    [Header("위저드 - 효과음 (3D)")]
    [SerializeField] private AudioClip swingSfx;
    #endregion

    private int sliceBoolHash, projectileBoolHash, spinBoolHash, spellBoolHash;
    private int slashStateHash, sliceStateHash, projectileStateHash, spinStateHash, spellStateHash;

    private float meleeCooldown;
    private float spinCooldown;
    private float projectileCooldown;
    private float spellCooldown;
    private bool nextMeleeIsSlice; // Slash → Slice → Slash … 번갈아

    private AudioSource sfxSource;
    private readonly HashSet<Player> struck = new HashSet<Player>();

    private BatWizardStatData WizardData => statData as BatWizardStatData;

    #region Properties (상태 클래스에서 사용)
    public float SkillGap => WizardData != null ? WizardData.SkillGap : 0.5f;
    public float MeleeRange => WizardData != null ? WizardData.MeleeRange : 2.2f;
    public float MeleeHitTime => WizardData != null ? WizardData.MeleeHitTime : 0.3f;
    public float MeleeDuration => WizardData != null ? WizardData.MeleeDuration : 0.6f;
    public float SpinRadius => WizardData != null ? WizardData.SpinRadius : 2.5f;
    public float SpinDuration => WizardData != null ? WizardData.SpinDuration : 1.0f;
    public float[] SpinHitTimes => WizardData != null && WizardData.SpinHitTimes != null && WizardData.SpinHitTimes.Length > 0
        ? WizardData.SpinHitTimes : new[] { 0.35f, 0.8f };
    public float ProjectileReleaseTime => WizardData != null ? WizardData.ProjectileReleaseTime : 0.3f;
    public float ProjectileDuration => WizardData != null ? WizardData.ProjectileDuration : 0.6f;
    public float SpellCastPoint => WizardData != null ? WizardData.SpellCastPoint : 0.4f;
    public float SpellDuration => WizardData != null ? WizardData.SpellDuration : 0.8f;
    public float SpellMinDistance => WizardData != null ? WizardData.SpellMinDistance : 4f;

    public bool IsMeleeReady => meleeCooldown <= 0f;
    public bool IsSpinReady => spinCooldown <= 0f;
    public bool IsProjectileReady => projectileCooldown <= 0f;
    public bool IsSpellReady => spellCooldown <= 0f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        sliceBoolHash = ToAnimHash(sliceBool);
        projectileBoolHash = ToAnimHash(projectileBool);
        spinBoolHash = ToAnimHash(spinBool);
        spellBoolHash = ToAnimHash(spellBool);
        slashStateHash = ToAnimHash(slashStateName);
        sliceStateHash = ToAnimHash(sliceStateName);
        projectileStateHash = ToAnimHash(projectileStateName);
        spinStateHash = ToAnimHash(spinStateName);
        spellStateHash = ToAnimHash(spellStateName);

        sfxSource = gameObject.GetOrAddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 1f;
        sfxSource.minDistance = 4f;
        sfxSource.maxDistance = 30f;
        sfxSource.rolloffMode = AudioRolloffMode.Linear;
    }

    // 풀에서 재사용될 때 쿨타임을 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        meleeCooldown = 0f;
        spinCooldown = 0f;
        projectileCooldown = 0f;
        spellCooldown = 0f;
        nextMeleeIsSlice = false;
    }

    protected override MonsterStateMachine CreateStateMachine()
        => new BatWizardStateMachine(this);

    // 위저드 전용 애니를 매핑에 더한다. Slash는 베이스 attackBool(Attack)을 쓴다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.Slice       => sliceBoolHash,
        MonsterAnimId.RangeAttack => projectileBoolHash,
        MonsterAnimId.Spin        => spinBoolHash,
        MonsterAnimId.CastSpell   => spellBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 휘두르는 소리는 클라에서도 나야 하므로 애니 변경 훅에서 재생한다.
    protected override void OnAnimChanged(MonsterAnimId animId)
    {
        if (animId == MonsterAnimId.Attack || animId == MonsterAnimId.Slice || animId == MonsterAnimId.Spin)
            PlaySfx(swingSfx);
    }

    // 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (meleeCooldown > 0f) meleeCooldown -= deltaTime;
        if (spinCooldown > 0f) spinCooldown -= deltaTime;
        if (projectileCooldown > 0f) projectileCooldown -= deltaTime;
        if (spellCooldown > 0f) spellCooldown -= deltaTime;
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
            MonsterAnimId.Attack      => slashStateHash,
            MonsterAnimId.Slice       => sliceStateHash,
            MonsterAnimId.RangeAttack => projectileStateHash,
            MonsterAnimId.Spin        => spinStateHash,
            MonsterAnimId.CastSpell   => spellStateHash,
            _ => 0,
        };
        if (hash == 0) return true;
        return Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
    }
    #endregion

    #region Skills
    public float DistanceToTarget() => Target != null ? PlanarDistanceToTarget() : float.MaxValue;

    public bool IsTargetInMeleeRange() => Target != null && PlanarDistanceToTarget() <= MeleeRange;

    /// <summary>이번 근접 공격 모션(Slash/Slice 번갈아)을 고르고 쿨을 건다.</summary>
    public MonsterAnimId NextMelee()
    {
        meleeCooldown = WizardData != null ? WizardData.MeleeCooldown : 1.2f;
        var anim = nextMeleeIsSlice ? MonsterAnimId.Slice : MonsterAnimId.Attack;
        nextMeleeIsSlice = !nextMeleeIsSlice;
        return anim;
    }

    public void StartSpinCooldown() => spinCooldown = WizardData != null ? WizardData.SpinCooldown : 7f;
    public void StartProjectileCooldown() => projectileCooldown = WizardData != null ? WizardData.ProjectileCooldown : 2.5f;
    public void StartSpellCooldown() => spellCooldown = WizardData != null ? WizardData.SpellCooldown : 9f;

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

    /// <summary>근접 판정: 현재 타깃 하나만. 거리(1.2배)·정면 각도 밖으로 피했으면 빗나간다.</summary>
    public void MeleeHit()
    {
        if (Target == null || !Target.IsAlive) return;
        if (PlanarDistanceToTarget() > MeleeRange * 1.2f) return;

        Vector3 to = Target.transform.position - transform.position;
        to.y = 0f;
        float half = (WizardData != null ? WizardData.MeleeAngle : 110f) * 0.5f;
        if (to.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, to) > half) return;

        DealDamage(Target, WizardData != null ? WizardData.MeleeDamage : 10f);
    }

    /// <summary>스핀 판정: 범위 안 플레이어 전원에게 1회씩. 마지막 타격이면 바깥으로 밀어낸다.</summary>
    public void SpinHit(bool last)
    {
        float dmg = WizardData != null ? WizardData.SpinDamage : 8f;
        CountPlayersInSpin();
        foreach (var player in struck)
        {
            DealDamage(player, dmg);
            if (last && WizardData != null && WizardData.SpinKnockback > 0f)
                player.Motor?.AddKnockback(player.transform.position - transform.position,
                    WizardData.SpinKnockback, WizardData.SpinKnockbackDuration);
        }
    }

    /// <summary>현재 타깃을 향해 마법탄을 쏜다.</summary>
    public void FireProjectile()
    {
        if (Target == null || WizardData == null) return;
        FireProjectileAsync(Target).Forget();
    }

    private async UniTaskVoid FireProjectileAsync(Player target)
    {
        var data = WizardData;
        var projectile = await Extensions.SpawnAsync<MonsterProjectile>(data.ProjectileKey);
        if (projectile == null) return;
        if (target == null || !target.isActiveAndEnabled)
        {
            Extensions.Despawn(projectile.gameObject);
            return;
        }

        Vector3 origin = transform.position + Vector3.up * data.MuzzleHeight + transform.forward * 0.4f;
        Vector3 dir = target.transform.position + Vector3.up * data.AimHeight - origin;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        int damage = Mathf.Max(Mathf.RoundToInt(data.ProjectileDamage), 0);
        projectile.Init(gameObject, origin, dir.normalized, data.ProjectileSpeed, damage, data.ProjectileLifeTime);
    }

    /// <summary>스펠: 타깃 발밑을 노려 운석을 순서대로 떨어뜨린다. 매번 그 순간의 타깃 위치를 다시 잡는다.</summary>
    public void CastMeteors()
    {
        if (Target == null || WizardData == null) return;
        CastMeteorsAsync(Target).Forget();
    }

    private async UniTaskVoid CastMeteorsAsync(Player target)
    {
        var data = WizardData;
        int damage = Mathf.Max(Mathf.RoundToInt(data.MeteorDamage), 0);
        var token = this.GetCancellationTokenOnDestroy();

        for (int i = 0; i < Mathf.Max(data.MeteorCount, 0); i++)
        {
            if (i > 0)
            {
                // 게임 시간 기준으로 기다린다(슬로우모션/일시정지 반영)
                float wait = data.MeteorInterval;
                while (wait > 0f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (GameScene.GameProcessing == GameProcessing.Processing) wait -= Time.deltaTime;
                }
            }

            // 위저드가 죽었거나 타깃이 사라졌으면 남은 운석은 취소
            if (!isActiveAndEnabled || Status == null || Status.IsDead) return;
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive) return;

            Vector3 impact = target.transform.position;
            var meteor = await Extensions.SpawnAsync<Meteor>(data.MeteorKey);
            if (meteor == null) continue;
            meteor.Init(gameObject, impact, damage, data.MeteorImpactRadius, data.MeteorWarningTime, data.MeteorLingerTime,
                slowMultiplier: data.MeteorSlowMultiplier, slowDuration: data.MeteorSlowDuration);
        }
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

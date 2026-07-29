using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 데몬. Mischief와 같은 근접+원거리 하이브리드 몬스터지만, 원거리 공격이 단일 투사체가 아니라
/// "자기 주변 반경 r 원 안 k개 지점에 운석을 떨구는" 광역 운석 세례(Meteor Storm)다.
/// - 운석 세례: 사거리(AttackRange)가 넓은 대신 내부 쿨타임(MeteorCooldown)이 길다.
/// - 슬래시: 내부 쿨이 없는 대신 사정거리(SlashRange)가 짧다. MinAttackPeriod 주기로만 제한된다.
/// 공격 선택은 DemonAttackState가, 시전/쿨타임 메카닉은 이 본체가 담당한다.
/// </summary>
public class Demon : Monster
{
    [Header("애니메이션 Bool 파라미터명 (컨트롤러 기준, 없으면 비워둠)")]
    [SerializeField] private string rangeAttackBool = "";
    [Tooltip("도약 애니메이션 Bool 파라미터명. 컨트롤러에 도약용 파라미터가 없으면 비워둔다(연출 생략, 이동은 정상)")]
    [SerializeField] private string jumpBool = "";

    [Header("도약(Leap) - 루트 모션 OFF, 스크립트로 실제 이동")]
    [Tooltip("도약 정점 높이(m). 루트가 이만큼 떠오르므로 콜라이더도 함께 올라간다")]
    [SerializeField] private float jumpHeight = 1.5f;
    [Tooltip("한 번 도약으로 타깃 쪽으로 이동하는 수평 거리(m)")]
    [SerializeField] private float jumpDistance = 3f;
    [Tooltip("도약 한 번에 걸리는 시간(초)")]
    [SerializeField] private float jumpDuration = 0.6f;
    [Tooltip("도약 사이의 재사용 대기시간(초)")]
    [SerializeField] private float jumpCooldown = 3f;

    private int rangeAttackBoolHash;
    private int jumpBoolHash;
    private float meteorCooldown;
    private float leapCooldown;

    private DemonStatData DemonData => statData as DemonStatData;

    #region Properties (상태 클래스에서 사용)
    /// <summary>근접 슬래시 사거리. AttackRange(운석 세례 사거리)보다 짧다.</summary>
    public float SlashRange => DemonData != null ? DemonData.SlashRange : 2f;
    /// <summary>운석 세례 내부 쿨타임이 끝나 시전 가능한지.</summary>
    public bool IsMeteorReady => meteorCooldown <= 0f;
    public float JumpHeight => jumpHeight;
    public float JumpDistance => jumpDistance;
    public float JumpDuration => jumpDuration;
    /// <summary>도약 재사용 대기가 끝나 다시 도약 가능한지.</summary>
    public bool IsLeapReady => leapCooldown <= 0f;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        rangeAttackBoolHash = ToAnimHash(rangeAttackBool);
        jumpBoolHash = ToAnimHash(jumpBool);
    }

    // 풀에서 재사용될 때 운석 세례/도약 쿨타임도 초기화한다.
    public override void OnSpawn()
    {
        base.OnSpawn();
        meteorCooldown = 0f;
        leapCooldown = 0f;
    }

    // 공격 상태를 DemonAttackState로 교체한 전용 머신 사용
    protected override MonsterStateMachine CreateStateMachine()
        => new DemonStateMachine(this);

    // 데몬 전용 애니(원거리 공격/도약)를 매핑에 더한다.
    protected override int AnimBoolHash(MonsterAnimId animId) => animId switch
    {
        MonsterAnimId.RangeAttack => rangeAttackBoolHash,
        MonsterAnimId.Jump        => jumpBoolHash,
        _ => base.AnimBoolHash(animId),
    };

    // 운석 세례/도약 쿨타임은 상태와 무관하게 항상 돈다.
    protected override void OnGameUpdate(float deltaTime)
    {
        if (meteorCooldown > 0f)
            meteorCooldown -= deltaTime;
        if (leapCooldown > 0f)
            leapCooldown -= deltaTime;

        base.OnGameUpdate(deltaTime);
    }

    /// <summary>타깃이 슬래시 사거리 안에 있는지.</summary>
    public bool IsTargetInSlashRange()
        => Target != null && PlanarDistanceToTarget() <= SlashRange;

    /// <summary>도약을 시작하며 재사용 대기시간을 건다.</summary>
    public void StartLeapCooldown() => leapCooldown = jumpCooldown;

    /// <summary>도약이 향할 수평 방향(단위 벡터). 타깃이 없으면 현재 전방.</summary>
    public Vector3 LeapDirection()
    {
        if (Target == null) return transform.forward;
        Vector3 d = Target.transform.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
    }

    /// <summary>
    /// 자기 주변 반경 MeteorRadius 원 안 MeteorCount개 지점에 운석 세례를 시전하고 내부 쿨타임을 시작한다.
    /// 쿨타임 중이거나 데이터가 없으면 무시된다.
    /// </summary>
    public void CastMeteorStorm()
    {
        if (!IsMeteorReady || DemonData == null) return;

        meteorCooldown = DemonData.MeteorCooldown;
        PlayAnim(MonsterAnimId.RangeAttack);
        // 시전 시점의 데몬 위치를 원의 중심으로 고정한다(이후 데몬이 움직여도 세례는 이 자리에 떨어진다).
        CastMeteorStormAsync(transform.position).Forget();
    }

    private async UniTaskVoid CastMeteorStormAsync(Vector3 center)
    {
        var data = DemonData;
        int count = Mathf.Max(data.MeteorCount, 0);
        if (count == 0) return;

        float rawDamage = data.MeteorDamage > 0f
            ? data.MeteorDamage
            : (status != null ? status.Attack : data.AttackDamage);
        int damage = Mathf.Max(Mathf.RoundToInt(rawDamage), 0);

        // "원 안에 적당히 분포": 각 운석을 겹치지 않는 각도 섹터에 하나씩 배치하고 섹터 내에서 각도를 흔들며,
        // 반지름은 √u 분포로 뽑아 (중심에 몰리지 않고) 원 전체에 고르게 퍼지도록 한다.
        float sectorStep = 360f / count;
        float sectorJitter = sectorStep * 0.5f;
        float stagger = Mathf.Max(data.MeteorSpawnStagger, 0f);

        for (int i = 0; i < count; i++)
        {
            float angleDeg = i * sectorStep + UnityEngine.Random.Range(-sectorJitter, sectorJitter);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float dist = data.MeteorRadius * Mathf.Sqrt(UnityEngine.Random.value);

            Vector3 impact = center + new Vector3(Mathf.Cos(angleRad) * dist, 0f, Mathf.Sin(angleRad) * dist);
            impact.y = center.y; // 데몬 발치 높이를 지면으로 간주

            // 운석마다 예고 시간을 늘려 순차적으로 착탄하게 한다(스태거 0이면 전부 동시).
            float warning = data.MeteorWarningTime + i * stagger;

            var meteor = await Extensions.SpawnAsync<Meteor>(data.MeteorKey);
            if (meteor == null) continue;

            meteor.Init(gameObject, impact, data.MeteorSpawnHeight, data.MeteorFallSpeed, damage, data.MeteorImpactRadius, warning);
        }
    }
}

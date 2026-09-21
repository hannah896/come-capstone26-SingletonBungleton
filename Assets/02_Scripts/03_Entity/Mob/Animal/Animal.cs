using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 비적대 Mob(사슴·닭·소 등). 평소 Idle ↔ 배회를 반복하고, 피격 시 넉백으로 튀어 오른 뒤 착지하면
/// 주변에 플레이어가 없어질 때까지 도망친다.
/// 상태 전환 결정은 AnimalStateMachine + 각 상태가, 실제 메카닉(NavMesh 이동/넉백/감지)은 이 본체가 담당한다.
///
/// 이동: 평소엔 NavMeshAgent + Kinematic Rigidbody. 넉백 중에만 Agent를 끄고 Rigidbody를 물리로 전환한다.
/// 네트워크: 호스트(또는 싱글)만 AI를 돌리고, 클라는 NetworkAnimalDirector가 보낸 위치/애니만 재현한다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public class Animal : Mob
{
    /// <summary>
    /// 풀에서 동물을 스폰하고 SO 스탯을 주입한 뒤 NavMesh 위에 배치해 반환한다.
    /// </summary>
    public static async UniTask<Animal> SpawnAsync(string key, AnimalStatData stat, Vector3 position, Transform parent = null)
    {
        var animal = await Extensions.SpawnAsync<Animal>(key, parent);
        if (animal == null) return null;

        if (stat != null) animal.ApplyStatData(stat);
        animal.PlaceAt(position);
        return animal;
    }

    #region Inspector
    [Header("네트워크")]
    [Tooltip("AnimalCatalog에 등록된 이 동물의 Addressable 키. " +
             "씬에 직접 배치한 동물을 멀티플레이에서 호스트가 복제 등록할 때 사용한다.")]
    [SerializeField] private string catalogKey = "";

    [Header("감지")]
    [Tooltip("위협(플레이어)을 탐지할 레이어. 비워두면(Everything) Player 컴포넌트로만 필터링")]
    [SerializeField] private LayerMask threatMask = ~0;

    [Header("넉백 / 착지")]
    [Tooltip("착지 판정에 쓸 지면 레이어")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("넉백 직후 이 시간(초) 동안은 착지 판정을 하지 않는다(뜨기 전에 바로 착지 처리되는 것 방지)")]
    [SerializeField] private float minAirTime = 0.15f;
    [Tooltip("이 시간(초)이 지나도 착지하지 못하면 강제로 착지 처리한다(끼임/낙하 안전장치)")]
    [SerializeField] private float maxAirTime = 3f;
    [Tooltip("발밑에서 지면까지 이 거리(m) 이내면 착지로 본다")]
    [SerializeField] private float groundCheckDistance = 0.25f;

    [Header("NavMesh")]
    [Tooltip("현재 위치에서 이 거리(m) 안의 NavMesh를 찾아 Agent를 붙인다")]
    [SerializeField] private float navSampleDistance = 3f;

    [Header("도망")]
    [Tooltip("도망 목적지를 한 번에 찍는 거리(m)")]
    [SerializeField] private float fleeStepDistance = 8f;
    [Tooltip("도망 중 목적지를 다시 계산하는 주기(초)")]
    [SerializeField] private float fleeRepathInterval = 0.5f;

    [Header("애니메이션 Bool 파라미터명 (항상 셋 중 하나만 true)")]
    [SerializeField] private string idleBool = "Idle";
    [SerializeField] private string walkBool = "Walk";
    [SerializeField] private string runBool = "Run";

    [Header("클라이언트 보간")]
    [Tooltip("호스트가 보낸 위치로 따라붙는 속도. 클수록 빠르게 스냅한다.")]
    [SerializeField] private float netLerpSpeed = 12f;
    [Tooltip("이 거리 이상 벌어지면 보간 없이 스냅한다(스폰 직후 대비).")]
    [SerializeField] private float netSnapDistance = 6f;
    #endregion

    private NavMeshAgent agent;
    private Rigidbody rb;
    private AnimalStateMachine stateMachine;

    // 마지막으로 공격해 온 위치(넉백/도망 방향 계산용)
    private Vector3 lastHitFrom;
    private bool hasHitFrom;

    // 마지막으로 때린 플레이어. 도망치는 동안 그 플레이어의 현재 위치 반대로 뛴다(쫓아와도 계속 멀어지도록).
    private Player lastAttacker;

    // 네트워크 보고로 들어온 공격자 위치에서 이 거리 안의 플레이어를 공격자로 본다
    private const float AttackerMatchDistance = 2f;

    // 풀에서 꺼낸 인스턴스인지. OnSpawn은 풀 스폰에서만 불리므로 false면 씬에 직접 배치된 동물이다.
    private bool isPooled;

    private readonly Collider[] threatBuffer = new Collider[16];
    private readonly RaycastHit[] groundBuffer = new RaycastHit[8];

    /// <summary>FleeRange 등 동물 전용 스탯 접근용.</summary>
    protected AnimalStatData AnimalData => statData as AnimalStatData;

    #region Properties (상태 클래스에서 사용)
    public float IdleDuration => AnimalData != null ? AnimalData.IdleDuration : 5f;
    public float WanderRadius => AnimalData != null ? AnimalData.WanderRadius : 8f;
    public float FleeRange => AnimalData != null ? AnimalData.FleeRange : 12f;
    public float MinFleeDuration => AnimalData != null ? AnimalData.MinFleeDuration : 4f;
    public float WalkSpeed => status != null ? status.MoveSpeed : 2f;
    public float RunSpeed => AnimalData != null ? AnimalData.RunSpeed : 6f;
    public float MinAirTime => minAirTime;
    public float MaxAirTime => maxAirTime;
    public float FleeRepathInterval => fleeRepathInterval;
    #endregion

    #region Network (호스트 권위 복제)
    /// <summary>
    /// 이 피어가 동물 AI를 직접 돌려야 하는지.
    /// 싱글플레이(방 미참가)이거나 호스트일 때만 true. 클라이언트는 호스트가 보낸 상태만 재현한다.
    /// </summary>
    public static bool IsSimulatedPeer
        => Main.Network == null || !Main.Network.IsInRoom || Main.Network.IsHost;

    /// <summary>NetworkAnimalDirector가 부여한 슬롯 번호. 미등록이면 -1.</summary>
    public int NetSlot { get; set; } = -1;

    /// <summary>현재 재생 중인 애니 ID. 호스트가 이 값을 복제하고 클라는 이걸 받아 재현한다.</summary>
    public AnimalAnimId CurrentAnimId { get; private set; } = AnimalAnimId.None;

    // 클라 전용: 호스트가 보낸 목표 위치/각도
    private Vector3 netTargetPos;
    private float netTargetYaw;
    private bool hasNetTarget;
    #endregion

    #region Lifecycle
    protected override void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        CacheAnimParams();
        ResetPhysics();

        base.Awake();
        stateMachine = new AnimalStateMachine(this);
    }

    /// <summary>status가 (재)생성될 때마다 OnDamaged를 구독해 피격 리액션을 건다.</summary>
    protected override void OnStatusInitialized()
    {
        base.OnStatusInitialized();
        if (status != null)
            status.OnDamaged += HandleDamaged;
    }

    // 풀에서 재사용될 때: 스탯(base) + 물리/네트워크/상태머신을 초기 상태로 되돌린다.
    public override void OnSpawn()
    {
        isPooled = true;
        base.OnSpawn();
        ResetPhysics();
        NetSlot = -1;
        hasNetTarget = false;
        hasHitFrom = false;
        lastAttacker = null;
        CurrentAnimId = AnimalAnimId.None;
        stateMachine = new AnimalStateMachine(this);
    }

    public override void OnDespawn()
    {
        base.OnDespawn();
        ResetPhysics();
    }

    protected override void OnGameUpdate(float deltaTime)
    {
        // 클라이언트는 AI를 돌리지 않는다. 돌리면 호스트가 보낸 위치/애니와 서로 싸운다.
        if (!IsSimulatedPeer)
        {
            // 씬에 직접 배치된 동물은 호스트의 복제본(디렉터가 풀에서 만든 것)으로 대체되므로 숨긴다.
            if (!isPooled)
            {
                gameObject.SetActive(false);
                return;
            }

            NetInterpolateStep(deltaTime);
            return;
        }

        TryRegisterScenePlaced();
        stateMachine.OnUpdate(deltaTime);
    }

    protected override void OnDeath()
        => stateMachine.ToDead();
    #endregion

    #region Damage
    public override void ApplyDamage(DamageContext context)
    {
        if (!CanDamage(context)) return;

        // 공격자 기억: 플레이어가 직접 때렸으면 그 플레이어, 네트워크 보고(Point = 공격자 위치)면 그 자리에 있는 플레이어.
        Player attacker = context.Instigator != null ? context.Instigator.GetComponentInParent<Player>() : null;
        lastHitFrom = attacker != null ? attacker.transform.position : context.Point;
        lastAttacker = attacker != null ? attacker : FindPlayerNear(context.Point, AttackerMatchDistance);
        hasHitFrom = true;

        base.ApplyDamage(context);
    }

    // 피격 시 넉백 상태로. 이번 공격으로 죽었다면 곧바로 OnDead가 이어지므로 넉백하지 않는다.
    private void HandleDamaged(float amount)
    {
        if (stateMachine == null || status == null || status.IsDead) return;
        stateMachine.ToKnockback();
    }

    /// <summary>사망 연출: 사슴 컨트롤러에 사망 클립이 없으므로 이동만 멈추고 Idle 포즈로 둔다.</summary>
    public override void PlayDeadAnim()
    {
        StopMoving();
        PlayAnim(AnimalAnimId.Idle);
    }
    #endregion

    #region NavMesh Locomotion
    /// <summary>지정 위치로 순간이동시키고 가능하면 NavMesh에 붙인다. 스폰 직후 사용.</summary>
    public void PlaceAt(Vector3 position)
    {
        if (agent.enabled) agent.enabled = false;
        transform.position = position;
        TryAttachAgent();
    }

    /// <summary>주변 NavMesh를 찾아 Agent를 켠다. 아직 NavMesh가 없으면(런타임 빌드 전) false.</summary>
    public bool TryAttachAgent()
    {
        if (agent.enabled && agent.isOnNavMesh) return true;
        if (!rb.isKinematic) return false; // 넉백으로 공중에 떠 있는 중

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navSampleDistance, agent.areaMask))
            return false;

        agent.enabled = true;
        agent.Warp(hit.position);
        return agent.isOnNavMesh;
    }

    /// <summary>목적지로 이동 시작. NavMesh에 붙지 못했거나 경로가 없으면 false.</summary>
    public bool MoveTo(Vector3 destination, float speed)
    {
        if (!TryAttachAgent()) return false;

        agent.speed = speed;
        agent.isStopped = false;
        return agent.SetDestination(destination);
    }

    public void StopMoving()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    /// <summary>목적지에 도착했거나 더 갈 수 없는 상태인지.</summary>
    public bool HasArrived()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return true;
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    /// <summary>현재 위치 기준 WanderRadius 안에서 NavMesh 위의 랜덤 지점을 고른다.</summary>
    public bool TryGetWanderPoint(out Vector3 point)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 offset = Random.insideUnitCircle * WanderRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, agent.areaMask))
            {
                point = hit.position;
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    /// <summary>
    /// 위협 반대 방향으로 fleeStepDistance만큼 떨어진 NavMesh 지점을 고른다.
    /// 정반대가 막혀 있으면(벽/바다) 좌우로 각도를 벌려가며 찾는다.
    /// </summary>
    public bool TryGetFleePoint(Vector3 threatPosition, out Vector3 point)
    {
        Vector3 away = transform.position - threatPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
        away.Normalize();

        float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };
        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
            Vector3 candidate = transform.position + dir * fleeStepDistance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, agent.areaMask))
            {
                point = hit.position;
                return true;
            }
        }

        point = transform.position;
        return false;
    }
    #endregion

    #region Threat
    /// <summary>FleeRange 안에서 살아있는 가장 가까운 플레이어를 찾는다. 없으면 null.</summary>
    public Player FindNearestThreat()
    {
        if (FleeRange <= 0f) return null;

        int count = Physics.OverlapSphereNonAlloc(transform.position, FleeRange, threatBuffer, threatMask, QueryTriggerInteraction.Collide);
        Player nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var player = threatBuffer[i].GetComponentInParent<Player>();
            if (player == null || !player.isActiveAndEnabled) continue;
            if (!player.IsAlive) continue;

            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = player;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 도망칠 기준 위치 = 마지막으로 때린 플레이어의 현재 위치. 그 플레이어를 못 찾으면 맞은 순간의 공격 위치.
    /// 이 위치의 반대 방향(맞은 방향의 반대)으로 뛴다.
    /// </summary>
    public Vector3 GetAttackerPosition()
    {
        if (lastAttacker != null && lastAttacker.isActiveAndEnabled && lastAttacker.IsAlive)
            return lastAttacker.transform.position;

        return lastHitFrom;
    }

    // position 근처(maxDistance 이내)에서 가장 가까운 살아있는 플레이어를 찾는다.
    private static Player FindPlayerNear(Vector3 position, float maxDistance)
    {
        Player best = null;
        float bestSqr = maxDistance * maxDistance;

        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (!player.isActiveAndEnabled || !player.IsAlive) continue;

            float sqr = (player.transform.position - position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            bestSqr = sqr;
            best = player;
        }

        return best;
    }

    /// <summary>도망 기준 위치: 주변 플레이어가 있으면 그 위치, 없으면 마지막 공격 위치.</summary>
    public bool TryGetThreatPosition(out Vector3 position)
    {
        Player threat = FindNearestThreat();
        if (threat != null)
        {
            position = threat.transform.position;
            return true;
        }

        position = lastHitFrom;
        return false;
    }
    #endregion

    #region Knockback
    /// <summary>Agent를 끄고 Rigidbody를 물리로 전환해 공격자 반대 방향 위로 튕겨 올린다.</summary>
    public void BeginKnockback()
    {
        StopMoving();
        agent.enabled = false;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;

        Vector3 away = hasHitFrom ? transform.position - lastHitFrom : -transform.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;

        float up = AnimalData != null ? AnimalData.KnockbackUpForce : 4f;
        float back = AnimalData != null ? AnimalData.KnockbackBackForce : 3f;
        rb.AddForce(Vector3.up * up + away * back, ForceMode.VelocityChange);
    }

    /// <summary>하강 중이고 발밑 가까이에 지면이 있는지.</summary>
    public bool IsGrounded()
    {
        if (rb.linearVelocity.y > 0.1f) return false;

        const float originOffset = 0.2f;
        int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * originOffset, Vector3.down, groundBuffer,
            originOffset + groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (groundBuffer[i].collider.attachedRigidbody == rb) continue; // 자기 콜라이더 제외
            return true;
        }

        return false;
    }

    /// <summary>착지 처리: 물리를 멈추고 다시 NavMesh Agent로 복귀한다.</summary>
    public void EndKnockback()
    {
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        TryAttachAgent();
    }

    // 평소 상태(Agent 대기 + Kinematic)로 되돌린다. Agent는 NavMesh가 확인될 때 TryAttachAgent가 켠다.
    private void ResetPhysics()
    {
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (agent != null && agent.enabled)
            agent.enabled = false;
    }
    #endregion

    #region Animation
    private int idleHash;
    private int walkHash;
    private int runHash;
    private readonly HashSet<int> animBoolHashes = new HashSet<int>();

    // 컨트롤러에 실제 존재하는 Bool만 기록한다. 없는 이름에 SetBool을 걸면 경고가 난다.
    private void CacheAnimParams()
    {
        idleHash = string.IsNullOrEmpty(idleBool) ? 0 : Animator.StringToHash(idleBool);
        walkHash = string.IsNullOrEmpty(walkBool) ? 0 : Animator.StringToHash(walkBool);
        runHash = string.IsNullOrEmpty(runBool) ? 0 : Animator.StringToHash(runBool);

        animBoolHashes.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool)
                animBoolHashes.Add(param.nameHash);
        }
    }

    /// <summary>
    /// Idle/Walk/Run Bool을 애니 ID에 맞게 켠다. 항상 셋 중 하나만 true가 되도록 한다.
    ///
    /// Idle Bool이 필요한 이유: 컨트롤러의 idle 상태는 Idle == false 일 때만 Exit로 빠진다.
    /// (예전처럼 조건 없이 ExitTime에만 기대면 idle 클립 11초가 거의 끝날 때까지 애니가 바뀌지 않아,
    ///  NavMeshAgent는 이미 걷는데 화면에서는 가만히 서서 미끄러진다)
    /// 셋 다 false가 되면 Entry가 갈 곳을 못 찾고 기본 상태(idle)와 Exit 사이를 오가므로 그런 조합을 만들지 말 것.
    /// </summary>
    public void PlayAnim(AnimalAnimId animId)
    {
        CurrentAnimId = animId;
        if (animator == null) return;

        SetBoolSafe(walkHash, animId == AnimalAnimId.Walk);
        SetBoolSafe(runHash, animId == AnimalAnimId.Run);
        SetBoolSafe(idleHash, animId != AnimalAnimId.Walk && animId != AnimalAnimId.Run);
    }

    private void SetBoolSafe(int hash, bool value)
    {
        if (hash == 0 || !animBoolHashes.Contains(hash)) return;
        animator.SetBool(hash, value);
    }
    #endregion

    #region Network
    // 씬에 직접 배치된 동물을 호스트가 복제 대상으로 등록한다(풀 스폰 동물은 스포너가 등록).
    private void TryRegisterScenePlaced()
    {
#if PHOTON_FUSION
        if (isPooled || NetSlot >= 0 || string.IsNullOrEmpty(catalogKey)) return;

        NetworkAnimalDirector director = NetworkAnimalDirector.Instance;
        if (director == null) return;

        byte catalogId = AnimalCatalog.Instance.GetCatalogId(catalogKey);
        if (catalogId == 0) return;

        director.RegisterAnimal(this, catalogId);
#endif
    }

    /// <summary>
    /// 호스트가 보낸 이 동물의 최신 상태를 반영한다(클라이언트 전용).
    /// NetworkAnimalDirector가 매 프레임 호출한다.
    /// </summary>
    public void ApplyNetState(Vector3 position, float yaw, AnimalAnimId animId)
    {
        netTargetPos = position;
        netTargetYaw = yaw;

        // 첫 수신은 보간 없이 그 자리에서 시작
        if (!hasNetTarget)
        {
            hasNetTarget = true;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        if (animId != CurrentAnimId)
            PlayAnim(animId);
    }

    // 호스트가 보낸 목표로 부드럽게 따라붙는다. 너무 벌어지면 스냅.
    private void NetInterpolateStep(float deltaTime)
    {
        if (!hasNetTarget) return;

        Quaternion targetRot = Quaternion.Euler(0f, netTargetYaw, 0f);

        if ((transform.position - netTargetPos).sqrMagnitude > netSnapDistance * netSnapDistance)
        {
            transform.SetPositionAndRotation(netTargetPos, targetRot);
            return;
        }

        float t = 1f - Mathf.Exp(-netLerpSpeed * deltaTime);
        transform.position = Vector3.Lerp(transform.position, netTargetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }
    #endregion
}

using UnityEngine;

/// <summary>청크와 별개로 유지되는 파괴 불가 플레이어 제단. 쿠션만 늘어나며 충돌체 크기는 유지한다.</summary>
[DisallowMultipleComponent]
public sealed class StartSofa : Structure, IDamageable
{
    public const float RestScaleY = 0.5f;
    public const float SpawnScaleY = 5f;

    [SerializeField] private Transform cushion;
    [SerializeField] private BoxCollider seatCollider;
    [Header("플레이어 스폰 연출")]
    [SerializeField, Min(0.1f)] private float fallHeight = 3.5f;
    [SerializeField, Min(0.1f)] private float fallDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float bounceDuration = 0.7f;
    [SerializeField, Min(0.1f)] private float bounceHeight = 1.5f;
    [Tooltip("소파 끝과 플레이어 콜라이더 사이의 착지 여유 거리")]
    [SerializeField, Min(0.1f)] private float landingPadding = 0.6f;
    [SerializeField, Min(0.1f)] private float cushionStretchDuration = 0.35f;

    private float stretchRemaining;

    public Bounds SeatBounds => seatCollider != null
        ? seatCollider.bounds : new Bounds(transform.position, new Vector3(2.53f, 0.13f, 2.6f));
    public float FallHeight => Mathf.Max(0.1f, fallHeight);
    public float FallDuration => Mathf.Max(0.1f, fallDuration);
    public float BounceDuration => Mathf.Max(0.1f, bounceDuration);
    public float BounceHeight => Mathf.Max(0.1f, bounceHeight);
    // 쿠션이 순간적으로 늘어나는 만큼 발도 올려, 커진 쿠션에 몸이 파묻히지 않도록 한다.
    public float StretchLift => cushion != null
        ? Mathf.Max(0f, SeatBounds.max.y - cushion.position.y) * (SpawnScaleY / RestScaleY - 1f)
        : 0f;

    private void Awake()
    {
        if (seatCollider == null) seatCollider = GetComponent<BoxCollider>();
        if (cushion == null) cushion = transform.Find("P_PROP_sofa_cushion_interior_01");
        SetCushionScale(RestScaleY);
    }

    private void OnEnable() => Main.Loop.OnUpdate += TickCushion;

    private void OnDisable()
    {
        Main.Loop.OnUpdate -= TickCushion;
        stretchRemaining = 0f;
        SetCushionScale(RestScaleY);
    }

    // 게임상의 모든 데미지/철거 경로에서 제단을 보호한다.
    public override bool CanInteract(InteractionContext context) => false;
    public override bool CanDemolish() => false;
    public override void Demolish() { }
    public bool CanDamage(DamageContext context) => false;
    public void ApplyDamage(DamageContext context) { }

    /// <summary>플레이어 캡슐 하단이 떨어지기 시작할 위치. 실제 피벗 보정은 연출이 담당한다.</summary>
    public Vector3 GetSpawnPosition()
    {
        Bounds seat = SeatBounds;
        return new Vector3(seat.center.x, seat.max.y + FallHeight, seat.center.z);
    }

    /// <summary>월드 +X 방향으로 소파를 벗어난 지면. 부활/구조 이동도 이 지점을 사용한다.</summary>
    public Vector3 GetSafeReturnPosition(Player player)
    {
        CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
        Vector3 centerOffset = controller != null ? player.transform.TransformVector(controller.center) : Vector3.zero;
        float radius = controller != null
            ? controller.radius * Mathf.Max(Mathf.Abs(player.transform.lossyScale.x), Mathf.Abs(player.transform.lossyScale.z))
            : 0.3f;
        Bounds seat = SeatBounds;
        Vector3 point = new Vector3(seat.max.x + radius + Mathf.Max(0.1f, landingPadding), transform.position.y, seat.center.z);
        int groundMask = LayerMask.GetMask("Ground");
        if (Physics.Raycast(point + Vector3.up * 64f, Vector3.down, out RaycastHit hit,
            128f, groundMask, QueryTriggerInteraction.Ignore))
            point.y = hit.point.y;
        else
        {
            WorldLogicData logic = WorldGenManager.Instance != null ? WorldGenManager.Instance.CurrentLogicData : null;
            if (logic != null) point.y = logic.GetHeightAt(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.z));
        }
        point.x -= centerOffset.x;
        point.z -= centerOffset.z;
        point.y += 0.03f - GetFeetOffset(player);
        return point;
    }

    public static float GetFeetOffset(Player player)
    {
        if (player == null || !player.TryGetComponent(out CharacterController controller)) return 0f;
        return player.transform.TransformVector(controller.center).y
            - controller.height * Mathf.Abs(player.transform.lossyScale.y) * 0.5f;
    }

    /// <summary>각 피어에서 충돌체를 움직이지 않고 같은 쿠션 연출을 재생한다.</summary>
    public void PlayBounce()
    {
        stretchRemaining = Mathf.Max(0.1f, cushionStretchDuration);
        SetCushionScale(SpawnScaleY);
    }

    private void TickCushion(float deltaTime)
    {
        if (stretchRemaining <= 0f || deltaTime <= 0f) return;
        if (GameScene.GameProcessing == GameProcessing.Stopping ||
            (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing))) return;
        stretchRemaining = Mathf.Max(0f, stretchRemaining - deltaTime);
        float t = 1f - stretchRemaining / Mathf.Max(0.1f, cushionStretchDuration);
        // 충격 순간 Y=5, 짧은 복원 뒤에는 항상 Y=0.5로 돌아온다.
        SetCushionScale(Mathf.Lerp(SpawnScaleY, RestScaleY, t * t * (3f - 2f * t)));
    }

    private void SetCushionScale(float scaleY)
    {
        if (cushion == null) return;
        Vector3 scale = cushion.localScale;
        scale.y = scaleY;
        cushion.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 previous = transform.position + Vector3.right * WorldSpawnArea.DefaultClearRadius;
        for (int i = 1; i <= 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * WorldSpawnArea.DefaultClearRadius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(GetSpawnPosition(), new Vector3(SeatBounds.center.x, SeatBounds.max.y, SeatBounds.center.z));
    }
}

/// <summary>入力・重力に左右されない、落下と短い +X 放物線のスポーン演出。</summary>
internal sealed class PlayerSpawnIntro
{
    private readonly Player player;
    private readonly StartSofa sofa;
    private readonly Vector3 start;
    private readonly Vector3 impact;
    private readonly Vector3 landing;
    private float elapsed;
    private bool bounced;

    public PlayerSpawnIntro(Player player, StartSofa sofa)
    {
        this.player = player;
        this.sofa = sofa;
        Vector3 centerOffset = player.TryGetComponent(out CharacterController controller)
            ? player.transform.TransformVector(controller.center) : Vector3.zero;
        start = sofa.GetSpawnPosition();
        start.x -= centerOffset.x;
        start.z -= centerOffset.z;
        start.y += 0.03f - StartSofa.GetFeetOffset(player);
        impact = start - Vector3.up * sofa.FallHeight;
        landing = sofa.GetSafeReturnPosition(player);
        player.Motor.Teleport(start);
    }

    /// <returns>연출 중에는 true, 착지하면 false.</returns>
    public bool Tick(float deltaTime)
    {
        if (sofa == null) return false;
        elapsed += Mathf.Max(0f, deltaTime);
        if (elapsed < sofa.FallDuration)
        {
            float t = elapsed / sofa.FallDuration;
            player.Motor.Teleport(Vector3.Lerp(start, impact, t * t));
            return true;
        }

        if (!bounced)
        {
            bounced = true;
#if PHOTON_FUSION
            if (player.TryGetComponent(out NetworkPlayerSync sync) && sync.Object != null && sync.Object.IsValid)
                sync.BroadcastSpawnBounce();
            else
#endif
                sofa.PlayBounce();
        }

        float bounceT = Mathf.Clamp01((elapsed - sofa.FallDuration) / sofa.BounceDuration);
        Vector3 position = Vector3.Lerp(impact, landing, bounceT);
        position.y += sofa.StretchLift * (1f - bounceT)
            + 4f * sofa.BounceHeight * bounceT * (1f - bounceT);
        player.Motor.Teleport(position);
        return bounceT < 1f;
    }
}

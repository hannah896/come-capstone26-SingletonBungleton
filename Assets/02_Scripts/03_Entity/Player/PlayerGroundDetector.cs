using UnityEngine;

/// <summary>
/// SphereCast 기반 지면/경사 감지.
/// 발밑 지면까지의 거리(Gap)와 경사만 측정하고, "착지했다"는 판정은 PlayerMotor가 이 값과 수직 속도로 내린다.
/// (감지 거리 자체를 착지로 쓰면 공중에서 미리 착지 처리되어 낙하가 느려지고 경사를 타고 미끄러진다)
/// </summary>
public class PlayerGroundDetector
{
    private readonly CharacterController cc;
    private readonly Transform transform;

    // 설정값
    // 발밑을 훑는 최대 거리 — 내리막·계단에서 발을 지면에 붙일 때(스냅) 쓰는 범위다. 착지 판정은 더 짧은 GroundedTolerance를 쓴다.
    private readonly float snapDistance = 0.5f;
    // 캡슐 하단과 지면 사이가 이 값 이하면 발이 닿은 것으로 본다
    private float GroundedTolerance => cc.skinWidth + 0.05f;
    // 오를 수 있는 최대 경사 — CharacterController.slopeLimit(프리팹 값)을 그대로 따른다.
    // 따로 두면 한쪽만 올렸을 때 CC는 오르는데 여기서 미끄러뜨리는 식으로 서로 어긋난다.
    private float MaxSlopeAngle => cc.slopeLimit;
    private readonly LayerMask groundLayer;

    private RaycastHit groundHit;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    /// <summary>스냅 범위(snapDistance) 안에 지면이 있는지.</summary>
    public bool HasGround { get; private set; }

    /// <summary>캡슐 하단에서 지면까지의 거리. HasGround가 false면 의미 없음.</summary>
    public float Gap { get; private set; }

    /// <summary>발이 지면에 닿아 있는지 (거리 기준 또는 직전 Move에서 아래쪽 충돌).</summary>
    public bool HasContact => HasGround && (Gap <= GroundedTolerance || (cc.collisionFlags & CollisionFlags.Below) != 0);

    public float SlopeAngle { get; private set; }
    public Vector3 GroundNormal { get; private set; }

    /// <summary>발밑 지면이 오를 수 있는 경사인지.</summary>
    public bool IsWalkable => HasGround && SlopeAngle <= MaxSlopeAngle;
    public bool IsOnSlope => IsWalkable && SlopeAngle > 0.1f;

    /// <summary>
    /// 오를 수 없는 가파른 면 위에 실제로 서 있는지.
    /// 거리만으로 판정하면 떨어지다 옆 절벽을 스쳐도 켜지므로, 직전 Move에서 아래쪽 충돌이 있었을 때만 인정한다.
    /// </summary>
    public bool IsOnSteepSlope => HasGround && SlopeAngle > MaxSlopeAngle && (cc.collisionFlags & CollisionFlags.Below) != 0;

    /// <summary>스냅 범위 최대값 (모터가 내리막 붙이기 거리로 사용).</summary>
    public float SnapDistance => snapDistance;

    public PlayerGroundDetector(CharacterController cc, Transform transform, LayerMask groundLayer)
    {
        this.cc = cc;
        this.transform = transform;
        this.groundLayer = groundLayer;
        GroundNormal = Vector3.up;
    }

    public void Update(float deltaTime)
    {
        float radius = cc.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * cc.center.y;

        // 캡슐 하단보다 snapDistance만큼 아래까지 훑는다.
        // 기준은 center.y가 아니라 height/2 — 캡슐 하단은 center.y - height/2이므로,
        // origin(center.y)에서 캡슐 하단까지의 거리가 곧 height/2다.
        float bottomOffset = cc.height * 0.5f - radius;
        float maxDistance = bottomOffset + cc.skinWidth + snapDistance;

        if (TryCastGround(origin, radius, maxDistance, out groundHit))
        {
            HasGround = true;
            Gap = Mathf.Max(0f, groundHit.distance - bottomOffset);
            GroundNormal = groundHit.normal;
            SlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
        }
        else
        {
            HasGround = false;
            Gap = float.MaxValue;
            GroundNormal = Vector3.up;
            SlopeAngle = 0f;
        }
    }

    // 발밑으로 구를 쏴서 가장 가까운 지면을 찾는다. 자기 캐릭터에 붙은 콜라이더(장착 아이템 등)는 지면으로 치지 않는다.
    private bool TryCastGround(Vector3 origin, float radius, float maxDistance, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, hitBuffer, maxDistance, groundLayer, QueryTriggerInteraction.Ignore);

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;

            // 시작부터 겹친 콜라이더(distance 0, point 없음)는 발밑 지면으로 판단할 수 없어 제외
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue;

            if (!found || hit.distance < nearest.distance)
            {
                nearest = hit;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 이동 벡터를 경사면에 투영
    /// </summary>
    public Vector3 ProjectOnSlope(Vector3 moveDir)
    {
        return Vector3.ProjectOnPlane(moveDir, GroundNormal).normalized * moveDir.magnitude;
    }

    /// <summary>
    /// 가파른 경사면에서의 미끄러짐 벡터
    /// </summary>
    public Vector3 GetSteepSlopeSlideDirection()
    {
        return Vector3.ProjectOnPlane(Vector3.down, GroundNormal).normalized;
    }
}

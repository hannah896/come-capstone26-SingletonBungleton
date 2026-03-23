using UnityEngine;

/// <summary>
/// SphereCast 기반 지면/경사 감지
/// </summary>
public class PlayerGroundDetector
{
    private readonly CharacterController cc;
    private readonly Transform transform;

    // 설정값
    private readonly float checkDistance = 0.3f;
    private readonly float maxSlopeAngle = 45f;
    private readonly float coyoteTimeDuration = 0.15f;
    private readonly LayerMask groundLayer;

    // 상태
    private float timeSinceGrounded;
    private RaycastHit groundHit;

    public bool IsGrounded { get; private set; }
    public bool WasGroundedRecently => IsGrounded || timeSinceGrounded < coyoteTimeDuration;
    public float SlopeAngle { get; private set; }
    public bool IsOnSlope => SlopeAngle > 0.1f && SlopeAngle <= maxSlopeAngle;
    public bool IsSteepSlope => SlopeAngle > maxSlopeAngle;
    public Vector3 GroundNormal { get; private set; }

    public PlayerGroundDetector(CharacterController cc, Transform transform, LayerMask groundLayer)
    {
        this.cc = cc;
        this.transform = transform;
        this.groundLayer = groundLayer;
        GroundNormal = Vector3.up;
    }

    public void Update(float deltaTime)
    {
        // SphereCast로 지면 감지
        float radius = cc.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * (cc.center.y);

        if (Physics.SphereCast(origin, radius, Vector3.down, out groundHit,
            cc.center.y - radius + cc.skinWidth + checkDistance, groundLayer))
        {
            IsGrounded = true;
            GroundNormal = groundHit.normal;
            SlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
            timeSinceGrounded = 0f;
        }
        else
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            SlopeAngle = 0f;
            timeSinceGrounded += deltaTime;
        }
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

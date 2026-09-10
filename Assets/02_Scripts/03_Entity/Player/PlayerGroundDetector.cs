using UnityEngine;

/// <summary>
/// SphereCast 기반 지면/경사 감지
/// 울퉁불퉁한 테레인도 안정적으로 감지
/// </summary>
public class PlayerGroundDetector
{
    private readonly CharacterController cc;
    private readonly Transform transform;

    // 설정값
    private float checkDistance = 0.5f;  // 0.3 → 0.5로 증가 (울퉁불퉁한 지형 대응)
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

        // 캡슐 하단보다 checkDistance만큼 아래까지 훑는다 (울퉁불퉁한 테레인 대응).
        // 기준은 center.y가 아니라 height/2 — 캡슐 하단은 center.y - height/2이므로,
        // origin(center.y)에서 캡슐 하단까지의 거리가 곧 height/2다.
        // center.y를 쓰면 "캡슐 하단 = 루트 피벗"인 프리팹에서만 우연히 맞고,
        // 캡슐이 피벗보다 위에 있는 프리팹(여성)에서는 그 차이만큼 과하게 훑어
        // 아직 공중인데 IsGrounded가 켜진다.
        float maxDistance = cc.height * 0.5f - radius + cc.skinWidth + checkDistance;

        if (Physics.SphereCast(origin, radius, Vector3.down, out groundHit, maxDistance, groundLayer))
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

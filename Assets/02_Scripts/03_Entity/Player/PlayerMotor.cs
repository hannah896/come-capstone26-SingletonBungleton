using UnityEngine;

/// <summary>
/// CharacterController 기반 이동/물리 총괄
/// 상태(State)는 "어디로, 얼마나 빠르게" 결정하고, Motor가 실제 이동을 수행한다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("지면 감지")]
    [SerializeField] private LayerMask groundLayer;

    [Header("가파른 경사면 미끄러짐")]
    [SerializeField] private float steepSlopeSlideSpeed = 5f;

    private CharacterController cc;
    private PlayerGroundDetector groundDetector;
    private PlayerGravity gravity;

    // 상태에서 설정하는 수평 속도
    private Vector3 moveVelocity;

    // 회전 관련
    private Vector3 pendingRotationDir;
    private float pendingRotationSpeed;

    #region 외부 접근 프로퍼티

    /// <summary> 전체 속도 (수평 + 수직) </summary>
    public Vector3 Velocity => cc != null ? cc.velocity : Vector3.zero;

    /// <summary> 현재 수직 속도 </summary>
    public float VerticalVelocity => gravity.CurrentVerticalVelocity;

    /// <summary> 지면 접촉 여부 </summary>
    public bool IsGrounded => groundDetector.IsGrounded;

    /// <summary> 코요테 타임 적용 지면 판정 </summary>
    public bool WasGroundedRecently => groundDetector.WasGroundedRecently;

    /// <summary> 경사면 위에 있는지 </summary>
    public bool IsOnSlope => groundDetector.IsOnSlope;

    /// <summary> 가파른 경사면인지 </summary>
    public bool IsSteepSlope => groundDetector.IsSteepSlope;

    /// <summary> 현재 경사 각도 </summary>
    public float SlopeAngle => groundDetector.SlopeAngle;

    #endregion

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        groundDetector = new PlayerGroundDetector(cc, transform, groundLayer);
        gravity = new PlayerGravity();
    }

    #region 상태에서 호출하는 메서드

    /// <summary>
    /// 수평 이동 속도를 설정한다 (월드 방향 기준)
    /// </summary>
    public void SetHorizontalVelocity(Vector3 worldDir, float speed)
    {
        if (worldDir.sqrMagnitude > 0.01f)
            moveVelocity = worldDir.normalized * speed;
        else
            moveVelocity = Vector3.zero;
    }

    /// <summary>
    /// 수직 속도를 직접 설정한다 (점프 시 사용)
    /// </summary>
    public void SetVerticalVelocity(float vy)
    {
        gravity.SetVelocity(vy);
    }

    /// <summary>
    /// 중력 활성/비활성 (사다리, 수영 등)
    /// </summary>
    public void SetGravityEnabled(bool enabled)
    {
        gravity.SetEnabled(enabled);
    }

    /// <summary>
    /// 이동 방향으로 부드럽게 회전
    /// </summary>
    public void RotateToward(Vector3 dir, float rotSpeed, float dt)
    {
        pendingRotationDir = dir;
        pendingRotationSpeed = rotSpeed;
    }

    #endregion

    /// <summary>
    /// 매 프레임 호출 — 지면 감지, 중력 적용, 최종 이동 수행
    /// </summary>
    public void Tick(float deltaTime)
    {
        // 1. 지면 감지
        groundDetector.Update(deltaTime);

        // 2. 중력 적용
        gravity.Update(deltaTime, IsGrounded);

        // 3. 최종 속도 계산
        Vector3 finalVelocity = moveVelocity;

        // 4. 경사면 보정
        if (IsOnSlope && gravity.CurrentVerticalVelocity <= 0f)
        {
            finalVelocity = groundDetector.ProjectOnSlope(finalVelocity);
        }

        // 5. 가파른 경사면 미끄러짐
        if (IsSteepSlope)
        {
            Vector3 slideDir = groundDetector.GetSteepSlopeSlideDirection();
            finalVelocity += slideDir * steepSlopeSlideSpeed;

            // 위로 이동 차단
            float dot = Vector3.Dot(moveVelocity, Vector3.ProjectOnPlane(Vector3.up, groundDetector.GroundNormal));
            if (dot > 0f)
                finalVelocity = Vector3.ProjectOnPlane(finalVelocity, groundDetector.GroundNormal);
        }

        // 6. 수직 속도 합산
        finalVelocity.y = gravity.CurrentVerticalVelocity;

        // 7. CharacterController 이동
        cc.Move(finalVelocity * deltaTime);

        // 8. 회전 처리
        ApplyRotation(deltaTime);

        // 9. 다음 프레임을 위해 수평 속도 초기화
        moveVelocity = Vector3.zero;
    }

    private void ApplyRotation(float deltaTime)
    {
        if (pendingRotationDir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(pendingRotationDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, pendingRotationSpeed * deltaTime);

        pendingRotationDir = Vector3.zero;
    }

    private void ApplyPosition(float deltaTime)
    {

    }
}

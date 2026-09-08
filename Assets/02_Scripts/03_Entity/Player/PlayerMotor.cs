using UnityEngine;

/// <summary>
/// CharacterController 기반 이동/물리 총괄
/// 상태(State)는 "어디로, 얼마나 빠르게" 결정하고, Motor가 실제 이동을 수행한다.
/// 공중에서는 수평 속도가 누적되어 관성이 있다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("지면 감지")]
    [SerializeField] private LayerMask groundLayer;

    [Header("가파른 경사면 미끄러짐")]
    [SerializeField] private float steepSlopeSlideSpeed = 5f;

    [Header("공중 가속")]
    [SerializeField] private float airAcceleration = 2f;  // 공중에서의 가속 (관성 시뮬레이션)
    [SerializeField] private float airDamping = 0.95f;    // 공중에서의 감속 (공기 저항)

    private CharacterController cc;
    private PlayerGroundDetector groundDetector;
    private PlayerGravity gravity;

    // 상태에서 설정하는 수평 속도
    private Vector3 moveVelocity;
    
    // 공중에서 누적되는 수평 속도 (관성)
    private Vector3 airVelocity;

    // 외부 충격(넉백). 상태 이동과 별개로 합산되며 시간에 따라 감쇠한다.
    private Vector3 knockbackVelocity;
    private float knockbackDuration;
    private float knockbackRemaining;

    // 회전 관련
    private Vector3 pendingRotationDir;
    private float pendingRotationSpeed;

    #region 외부 접근 프로퍼티

    // 전체 속도 (수평 + 수직)
    public Vector3 Velocity => cc != null ? cc.velocity : Vector3.zero;

    // 현재 수직 속도
    public float VerticalVelocity => gravity != null ? gravity.CurrentVerticalVelocity : 0f;

    // 지면 접촉 여부
    public bool IsGrounded => groundDetector != null && groundDetector.IsGrounded;

    // 코요테 타임 적용 지면 판정
    public bool WasGroundedRecently => groundDetector != null && groundDetector.WasGroundedRecently;
    public bool CanJump => cc != null && cc.isGrounded;

    // 경사면 위에 있는지
    public bool IsOnSlope => groundDetector != null && groundDetector.IsOnSlope;

    // 가파른 경사면인지
    public bool IsSteepSlope => groundDetector != null && groundDetector.IsSteepSlope;

    // 현재 경사 각도
    public float SlopeAngle => groundDetector != null ? groundDetector.SlopeAngle : 0f;

    #endregion

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        groundDetector = new PlayerGroundDetector(cc, transform, groundLayer);
        gravity = new PlayerGravity();
        airVelocity = Vector3.zero;
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
    public void RotateToward(Vector3 dir, float rotSpeed)
    {
        pendingRotationDir = dir;
        pendingRotationSpeed = rotSpeed;
    }

    /// <summary>
    /// 지정 위치로 즉시 이동시킨다. (부활·리스폰 등)
    /// CharacterController는 자체 내부 좌표를 갖고 있어 transform만 옮기면 다음 Move에서 되돌려지므로,
    /// 비활성화 → 위치 변경 → 재활성화 순서로 처리한다.
    /// </summary>
    public void Teleport(Vector3 position)
    {
        if (cc == null)
        {
            transform.position = position;
            ResetVelocity();
            return;
        }

        bool wasEnabled = cc.enabled;
        cc.enabled = false;
        transform.position = position;
        cc.enabled = wasEnabled;

        ResetVelocity();
    }

    /// <summary>
    /// 누적된 이동·낙하 속도를 모두 초기화한다.
    /// (남은 낙하 속도를 들고 순간이동하면 착지 순간 지면을 뚫거나 튕긴다)
    /// </summary>
    public void ResetVelocity()
    {
        moveVelocity = Vector3.zero;
        airVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        knockbackRemaining = 0f;
        gravity?.SetVelocity(0f);
    }

    /// <summary>
    /// 외부 충격(넉백)을 가한다. 상태가 설정하는 이동 속도와 별개로 합산되며 duration 동안 선형 감쇠한다.
    /// (moveVelocity는 매 Tick 끝에서 초기화되므로 여기에 섞으면 한 프레임만 밀린다)
    /// </summary>
    public void AddKnockback(Vector3 worldDir, float force, float duration = 0.25f)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f || force <= 0f || duration <= 0f) return;

        knockbackVelocity = worldDir.normalized * force;
        knockbackDuration = duration;
        knockbackRemaining = duration;
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
        Vector3 finalVelocity = Vector3.zero;

        if (IsGrounded)
        {
            // 지면: 상태가 설정한 속도만 사용
            finalVelocity = moveVelocity;
            airVelocity = Vector3.zero;  // 공중 속도 리셋
            if (gravity.CurrentVerticalVelocity > 0f)
                airVelocity = moveVelocity;
        }
        else
        {
            // 공중: 입력 속도와 관성 속도를 합산
            // 입력 방향으로 점진적으로 가속 (airAcceleration)
            // 기존 속도에 감속 적용 (airDamping)
            
            airVelocity = Vector3.Lerp(airVelocity, moveVelocity, airAcceleration * deltaTime);
            airVelocity *= airDamping;  // 매 프레임 약간의 공기 저항
            
            finalVelocity = airVelocity;
        }

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

        // 5-1. 넉백 합산 (남은 시간에 비례해 선형 감쇠)
        if (knockbackRemaining > 0f)
        {
            knockbackRemaining -= deltaTime;
            float t = Mathf.Clamp01(knockbackRemaining / Mathf.Max(knockbackDuration, 0.0001f));
            finalVelocity += knockbackVelocity * t;

            if (knockbackRemaining <= 0f)
                knockbackVelocity = Vector3.zero;
        }

        // 6. 수직 속도 합산
        finalVelocity.y = gravity.CurrentVerticalVelocity;

        // 7. CharacterController 이동
        cc.Move(finalVelocity * deltaTime);

        // 8. 회전 처리
        ApplyRotation(deltaTime);

        // 9. 다음 프레임을 위해 수평 입력 속도 초기화 (상태가 매 프레임 설정)
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
}

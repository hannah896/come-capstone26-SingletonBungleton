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
    [Tooltip("지형 외에 올라설 수 있는 레이어 (돌 같은 자원·구조물 등). groundLayer와 합쳐서 발밑을 감지한다.\n" +
             "기본값은 Ignore Raycast / Water / UI / ViewModel을 뺀 전부 — 자원 프리팹마다 레이어가 달라 하나씩 넣으면 빠지는 경우가 생긴다")]
    [SerializeField] private LayerMask standableLayers = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 8));

    [Header("가파른 경사면 미끄러짐")]
    [SerializeField] private float steepSlopeSlideSpeed = 5f;

    [Header("공중 이동")]
    [Tooltip("공중에서 입력 방향으로 속도를 트는 빠르기(초당). 클수록 공중 조작이 민첩하다")]
    [SerializeField] private float airControl = 6f;
    [Tooltip("공중 수평 속도의 공기 저항(초당 감쇠율). 0이면 관성이 그대로 유지된다")]
    [SerializeField] private float airDrag = 0.2f;

    [Header("착지")]
    [Tooltip("땅을 벗어난 뒤에도 점프/지상 판정을 유지하는 시간(초)")]
    [SerializeField] private float coyoteTime = 0.15f;

    private CharacterController cc;
    private PlayerGroundDetector groundDetector;
    private PlayerGravity gravity;

    // 상태에서 설정하는 수평 속도
    private Vector3 moveVelocity;
    
    // 공중에서 누적되는 수평 속도 (관성)
    private Vector3 airVelocity;

    // 마지막으로 서 있던 프레임의 수평 속도 — 땅을 떠나는 순간 공중 관성으로 넘겨준다
    private Vector3 lastGroundVelocity;

    // 착지 판정 (Tick에서 갱신)
    private bool isGrounded;
    private bool wasGrounded;
    private float timeSinceGrounded = float.MaxValue;

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

    // 지면에 서 있는지 (실제 접촉 또는 내리막 스냅). 올라가는 중에는 false.
    public bool IsGrounded => isGrounded;

    // 코요테 타임 적용 지면 판정
    public bool WasGroundedRecently => isGrounded || timeSinceGrounded < coyoteTime;

    // 서 있거나 막 발을 뗐고(코요테), 이미 위로 뜨는 중이 아니면 점프 가능
    public bool CanJump => WasGroundedRecently && VerticalVelocity <= 0f;

    // 경사면 위에 서 있는지
    public bool IsOnSlope => isGrounded && groundDetector != null && groundDetector.IsOnSlope;

    // 오를 수 없는 가파른 면 위에 서 있는지
    public bool IsSteepSlope => groundDetector != null && groundDetector.IsOnSteepSlope;

    // 현재 경사 각도
    public float SlopeAngle => groundDetector != null ? groundDetector.SlopeAngle : 0f;

    #endregion

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        groundDetector = new PlayerGroundDetector(cc, transform, groundLayer | standableLayers);
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
        lastGroundVelocity = Vector3.zero;
        // 순간이동 직후 이전 위치의 착지 판정이 다음 Tick까지 남지 않도록 공중 상태로 초기화한다
        isGrounded = false;
        wasGrounded = false;
        timeSinceGrounded = float.MaxValue;
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
    /// 매 프레임 호출 — 지면 감지, 착지 판정, 중력 적용, 최종 이동 수행
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 1. 지면 감지
        groundDetector.Update(deltaTime);

        // 2. 착지 판정
        //    - 올라가는 중(점프 직후)은 지면 근처여도 공중이다
        //    - 실제로 발이 닿고(짧은 허용 거리) 걸을 수 있는 경사일 때만 착지.
        //      감지 거리(0.5m)만으로 착지 처리하면 떨어지는 도중 낙하 속도가 -2로 묶여 경사를 타고 미끄러진다.
        //    - 내리막·계단: 직전 프레임에 서 있었고 발밑 가까이 걸을 수 있는 지면이 있으면 떨어뜨리지 않고 붙인다(스냅)
        bool rising = gravity.CurrentVerticalVelocity > 0f;
        bool contact = !rising && groundDetector.HasContact && groundDetector.IsWalkable;
        bool snap = !contact && !rising && wasGrounded
                    && groundDetector.IsWalkable && groundDetector.Gap <= groundDetector.SnapDistance;
        isGrounded = contact || snap;

        timeSinceGrounded = isGrounded ? 0f : timeSinceGrounded + deltaTime;

        // 땅을 떠나는 순간(점프든 걸어서 떨어지든) 지면에서 움직이던 수평 속도를 공중 관성으로 넘긴다
        if (wasGrounded && !isGrounded)
        {
            airVelocity = rising && moveVelocity.sqrMagnitude > lastGroundVelocity.sqrMagnitude
                ? moveVelocity
                : lastGroundVelocity;
        }

        // 3. 중력 적용 (서 있을 때만 지면 밀착용 미세 하강 속도로 고정)
        gravity.Update(deltaTime, isGrounded);

        // 4. 최종 속도 계산
        Vector3 finalVelocity;

        if (isGrounded)
        {
            // 지면: 상태가 설정한 속도를 경사면을 따라 적용
            Vector3 planar = moveVelocity;
            lastGroundVelocity = planar;
            airVelocity = Vector3.zero;

            finalVelocity = groundDetector.IsOnSlope ? groundDetector.ProjectOnSlope(planar) : planar;
            finalVelocity.y += gravity.CurrentVerticalVelocity;

            // 스냅: 발밑까지 남은 거리만큼 이번 프레임에 내려 붙인다 (내리막에서 공중으로 뜨지 않도록)
            if (snap)
                finalVelocity.y = Mathf.Min(finalVelocity.y, -groundDetector.Gap / deltaTime);
        }
        else
        {
            // 공중: 관성 유지 + 입력으로 방향 조절
            UpdateAirVelocity(deltaTime);
            finalVelocity = airVelocity;

            // 오를 수 없는 가파른 면 위: 위쪽 성분을 막고 아래로 미끄러뜨린다.
            // (중력은 그대로 적용 — 예전처럼 -2로 묶으면 절벽을 타고 천천히 미끄러져 내려간다)
            if (groundDetector.IsOnSteepSlope)
            {
                Vector3 slideDir = groundDetector.GetSteepSlopeSlideDirection();
                Vector3 uphill = Vector3.ProjectOnPlane(-slideDir, Vector3.up).normalized;
                float climb = Vector3.Dot(finalVelocity, uphill);
                if (climb > 0f)
                    finalVelocity -= uphill * climb;

                finalVelocity += slideDir * steepSlopeSlideSpeed;
            }

            finalVelocity.y += gravity.CurrentVerticalVelocity;
        }

        // 5. 넉백 합산 (남은 시간에 비례해 선형 감쇠)
        if (knockbackRemaining > 0f)
        {
            knockbackRemaining -= deltaTime;
            float t = Mathf.Clamp01(knockbackRemaining / Mathf.Max(knockbackDuration, 0.0001f));
            finalVelocity += knockbackVelocity * t;

            if (knockbackRemaining <= 0f)
                knockbackVelocity = Vector3.zero;
        }

        // 6. CharacterController 이동
        cc.Move(finalVelocity * deltaTime);

        // 7. 회전 처리
        ApplyRotation(deltaTime);

        // 8. 다음 프레임을 위해 수평 입력 속도 초기화 (상태가 매 프레임 설정)
        moveVelocity = Vector3.zero;
        wasGrounded = isGrounded;
    }

    /// <summary>
    /// 공중 수평 속도 갱신. 프레임레이트와 무관하게 초 단위로 계산한다.
    /// 입력은 방향을 트는 용도라, 이미 입력 속도보다 빠르게 날고 있으면 입력 때문에 감속하지 않는다.
    /// (예전 매 프레임 0.95 감쇠는 60fps에서 초당 95%가 사라져 공중 이동이 뻑뻑했다)
    /// </summary>
    private void UpdateAirVelocity(float deltaTime)
    {
        if (moveVelocity.sqrMagnitude > 0.0001f)
        {
            float speed = Mathf.Max(moveVelocity.magnitude, airVelocity.magnitude);
            Vector3 target = moveVelocity.normalized * speed;
            airVelocity = Vector3.Lerp(airVelocity, target, 1f - Mathf.Exp(-airControl * deltaTime));
        }

        airVelocity *= Mathf.Exp(-airDrag * deltaTime);
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

using UnityEngine;

/// <summary>
/// Rigidbody 없이 중력을 시뮬레이션하는 순수 C# 클래스
/// </summary>
public class PlayerGravity
{
    // 설정값
    private readonly float gravityAcceleration = -32f;
    private readonly float maxFallSpeed = -40f;
    private readonly float groundedPullDown = -2f;

    private bool enabled = true;

    public float CurrentVerticalVelocity { get; private set; }

    public void Update(float deltaTime, bool isGrounded)
    {
        if (!enabled)
            return;

        if (isGrounded && CurrentVerticalVelocity <= 0f)
        {
            // 지면 밀착용 미세 음수
            CurrentVerticalVelocity = groundedPullDown;
        }
        else
        {
            // 공중: 중력 가속
            CurrentVerticalVelocity += gravityAcceleration * deltaTime;
            if (CurrentVerticalVelocity < maxFallSpeed)
                CurrentVerticalVelocity = maxFallSpeed;
        }
    }

    /// <summary>
    /// 점프 시 초기 수직 속도 설정
    /// </summary>
    public void SetVelocity(float vy)
    {
        CurrentVerticalVelocity = vy;
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        if (!enabled)
            CurrentVerticalVelocity = 0f;
    }
}

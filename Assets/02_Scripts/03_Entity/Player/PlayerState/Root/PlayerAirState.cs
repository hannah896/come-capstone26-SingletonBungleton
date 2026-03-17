using UnityEngine;

/// <summary>
/// 플레이어 공중 상태 (Root)
/// Sub: Jump, Fall
/// </summary>
public class PlayerAirState : PlayerRootStateBase
{
    private float jumpForce = 5f;
    private bool jumpApplied = false;

    public PlayerAirState(PlayerRootStateMachine machine) : base(machine) { }

    public override void OnEnter()
    {
        base.OnEnter();
        Debug.Log("[State] Air 진입");
        jumpApplied = false;
        
        // 점프 힘 적용
        ApplyJump();
    }

    public override void OnExit()
    {
        base.OnExit();
        Debug.Log("[State] Air 퇴장");
    }

    public override void Update(float time = 1)
    {
        // 착지 감지 → Ground로 전환
        if (Entity.IsGrounded)
        {
            Machine.ChangeState(new PlayerGroundState(Machine));
            return;
        }
    }

    private void ApplyJump()
    {
        var rb = Entity.Rb;
        
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        Debug.Log($"[Jump] Applied jump force: {jumpForce}");
    }
}

using UnityEngine;

public class PlayerAnimData
{
    private Animator animator;
    private PlayerAnimHashKey animHashKey = new();

    public PlayerAnimHashKey AnimHashKey => animHashKey;

    public PlayerAnimData(Animator animator)
    {
        this.animator = animator;
    }

    /// <summary>
    /// Bool 파라미터를 사용해 애니메이션 전환.
    /// 로코모션 관련 bool을 모두 false로 리셋한 뒤 대상 파라미터만 true로 설정.
    /// </summary>
    public void PlayLocomotionAnimation(int animHash)
    {
        ResetLocomotionBools();
        animator.SetBool(animHash, true);
    }

    /// <summary>
    /// 지정된 Bool 파라미터를 false로 끔 (상태 퇴장 시 명시적으로 끌 때 사용)
    /// </summary>
    public void StopAnimation(int animHash)
    {
        animator.SetBool(animHash, false);
    }

    /// <summary>
    /// 로코모션 Bool 파라미터 전체 초기화
    /// </summary>
    private void ResetLocomotionBools()
    {
        animator.SetBool(animHashKey.Idle, false);
        animator.SetBool(animHashKey.Walk, false);
        animator.SetBool(animHashKey.Trace, false);
        animator.SetBool(animHashKey.Run, false);
        animator.SetBool(animHashKey.Jump, false);
    }
}

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
    /// Trigger 파라미터를 사용해 Action 애니메이션 재생.
    /// 로코모션 Bool을 모두 false로 리셋한 뒤 트리거를 발동.
    /// </summary>
    public void PlayActionAnimation(int animHash)
    {
        ResetLocomotionBools();
        animator.SetTrigger(animHash);
    }

    /// <summary>
    /// Action Trigger 파라미터 리셋 (OnExit 시 미발동 트리거 제거용)
    /// </summary>
    public void ResetActionTrigger(int animHash)
    {
        animator.ResetTrigger(animHash);
    }

    /// <summary>
    /// 현재 레이어의 애니메이션이 완료되었는지 확인.
    /// normalizedTime >= 1 이고 전환 중이 아닌 경우 true.
    /// </summary>
    public bool IsActionAnimationCompleted(int layer = 0)
    {
        return !animator.IsInTransition(layer)
            && animator.GetCurrentAnimatorStateInfo(layer).normalizedTime >= 1f;
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

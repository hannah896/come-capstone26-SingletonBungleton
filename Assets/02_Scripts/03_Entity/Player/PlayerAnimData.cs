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
    /// 루트 상태 진입 시 호출. 모든 루트 라우팅 bool을 끄고 지정한 루트 bool만 켠다.
    /// 애니메이터 BaseLayer는 이 bool로 어느 루트 SM(Locomotion/Action/Attack/Hurt/Dead)으로 갈지 결정한다.
    /// </summary>
    public void SetRootState(int rootBoolHash)
    {
        animator.SetBool(animHashKey.Locomotion, false);
        animator.SetBool(animHashKey.Action, false);
        animator.SetBool(animHashKey.Attack, false);
        animator.SetBool(animHashKey.Hurt, false);
        animator.SetBool(animHashKey.Dead, false);
        animator.SetBool(rootBoolHash, true);
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
    /// 애니메이터의 현재 스테이트(또는 전환 중인 목적지 스테이트)가 지정 해시와 일치하는지 확인.
    /// 전환 중에는 목적지(next)도 검사하므로, 액션 종료 후 Locomotion으로 복귀가 시작되는 시점을 감지할 수 있다.
    /// </summary>
    public bool IsInState(int stateShortHash, int layer = 0)
    {
        if (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateShortHash)
            return true;
        if (animator.IsInTransition(layer)
            && animator.GetNextAnimatorStateInfo(layer).shortNameHash == stateShortHash)
            return true;
        return false;
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

using UnityEngine;

public class PlayerAnimData
{
    private Animator animator;
    private PlayerAnimHashKey animHashKey;

    public PlayerAnimHashKey AnimHashKey => animHashKey;

    public PlayerAnimData(Animator animator)
    {
        this.animator = animator;
        this.animHashKey = new PlayerAnimHashKey();
    }

    /// <summary>
    /// Locomotion 애니메이션 파라미터를 CrossFade로 재생
    /// </summary>
    public void PlayLocomotionAnimation(int animHash, float transitionDuration = 0.1f)
    {
        animator.CrossFadeInFixedTime(animHash, transitionDuration);
    }
}

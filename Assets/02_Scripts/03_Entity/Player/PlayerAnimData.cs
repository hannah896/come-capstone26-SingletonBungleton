using System;
using UnityEngine;

public class PlayerAnimData
{
    /// <summary>
    /// Trigger 파라미터가 발동될 때 발생 (파라미터 해시 전달).
    /// Trigger는 애니메이터에서 다시 읽어낼 수 없어 폴링이 불가능하므로,
    /// 멀티플레이 동기화(NetworkPlayerSync)가 이 이벤트로 발동 사실을 잡아 원격 피어에 전달한다.
    /// </summary>
    public event Action<int> OnTriggerPlayed;

    /// <summary>
    /// CrossFade로 상태를 직접 전환할 때 발생 (대상 상태 해시 전달).
    /// CrossFade는 애니메이터 파라미터로 표현되지 않아 Bool/Trigger 복제만으로는 원격에 재현되지 않는다.
    /// (사망 연출로 CrossFade한 뒤 부활 시 Idle로 CrossFade해도, 원격에는 사망 상태가 그대로 남는다)
    /// 그래서 발동 사실을 이 이벤트로 알려 NetworkPlayerSync가 원격 피어에서 같은 CrossFade를 재현한다.
    /// </summary>
    public event Action<int> OnCrossFadePlayed;

    private Animator animator;
    private PlayerAnimHashKey animHashKey = new();

    // 각 루트 SM의 진입 상태 해시 (CrossFade 타겟)
    private static readonly int s_idleHash         = Animator.StringToHash("Idle");
    private static readonly int s_damageHash        = Animator.StringToHash("HumanF@Damage01");
    private static readonly int s_combatDeath01Hash = Animator.StringToHash("HumanF@CombatDeath01");
    private static readonly int s_combatDeath02Hash = Animator.StringToHash("HumanF@CombatDeath02");

    private const float CrossFadeTime = 0.05f;

    // 원격 피어에서 같은 CrossFade를 재현하기 위한 대상 상태 목록.
    // 배열 인덱스를 네트워크로 주고받으므로(해시 대신 1바이트), 새 CrossFade 대상은 반드시 여기에 추가한다.
    private static readonly int[] s_crossFadeStates =
    {
        s_idleHash,
        s_damageHash,
        s_combatDeath01Hash,
        s_combatDeath02Hash,
    };

    public PlayerAnimHashKey AnimHashKey => animHashKey;

    /// <summary>CrossFade 전환 시간 (원격 재현도 같은 값을 쓴다).</summary>
    public static float CrossFadeDuration => CrossFadeTime;

    /// <summary>CrossFade 대상 상태의 동기화 인덱스를 구한다. 목록에 없으면 -1.</summary>
    public static int IndexOfCrossFadeState(int stateHash)
    {
        for (int i = 0; i < s_crossFadeStates.Length; i++)
        {
            if (s_crossFadeStates[i] == stateHash) return i;
        }
        return -1;
    }

    /// <summary>동기화 인덱스에 해당하는 CrossFade 대상 상태 해시를 구한다. 범위를 벗어나면 0.</summary>
    public static int GetCrossFadeState(int index)
        => index >= 0 && index < s_crossFadeStates.Length ? s_crossFadeStates[index] : 0;

    public PlayerAnimData(Animator animator)
    {
        this.animator = animator;
    }

    /// <summary>
    /// 루트 상태 진입 시 호출. 모든 루트 라우팅 bool을 끄고 지정한 루트 bool만 켠다.
    /// CrossFade로 현재 애니메이션의 exit time을 무시하고 즉시 전환한다.
    /// </summary>
    public void SetRootState(int rootBoolHash)
    {
        animator.SetBool(animHashKey.Locomotion, false);
        animator.SetBool(animHashKey.Action, false);
        animator.SetBool(animHashKey.Attack, false);
        animator.SetBool(animHashKey.Hurt, false);
        animator.SetBool(animHashKey.Dead, false);
        animator.SetBool(rootBoolHash, true);

        int entryHash = GetEntryStateHash(rootBoolHash);
        if (entryHash != 0)
            PlayCrossFade(entryHash);
    }

    /// <summary>
    /// Dead 전용. wasHit에 따라 다른 사망 애니메이션으로 즉시 CrossFade한다.
    /// Entry 조건 평가를 거치지 않고 직접 타겟 상태로 전환하므로 Hit 트리거 불필요.
    /// </summary>
    public void PlayDeadAnimation(bool wasHit)
    {
        animator.SetBool(animHashKey.Locomotion, false);
        animator.SetBool(animHashKey.Action, false);
        animator.SetBool(animHashKey.Attack, false);
        animator.SetBool(animHashKey.Hurt, false);
        animator.SetBool(animHashKey.Dead, true);

        int deathHash = wasHit ? s_combatDeath01Hash : s_combatDeath02Hash;
        PlayCrossFade(deathHash);
    }

    /// <summary>
    /// 지정 상태로 CrossFade하고, 원격 재현을 위해 <see cref="OnCrossFadePlayed"/>를 발행한다.
    /// </summary>
    public void PlayCrossFade(int stateHash)
    {
        animator.CrossFade(stateHash, CrossFadeTime, 0, 0f);
        OnCrossFadePlayed?.Invoke(stateHash);
    }

    private int GetEntryStateHash(int rootBoolHash)
    {
        if (rootBoolHash == animHashKey.Locomotion) return s_idleHash;
        if (rootBoolHash == animHashKey.Hurt)       return s_damageHash;
        return 0; // Action/Attack/Sleep 은 bool + 자체 Trigger로 처리
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
        OnTriggerPlayed?.Invoke(animHash);
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
    /// Hit 트리거를 설정한다. Dead SM 진입 전에 호출해야 CombatDeath01로 라우팅된다.
    /// </summary>
    public void SetHitTrigger()
    {
        animator.SetTrigger(animHashKey.Hit);
        OnTriggerPlayed?.Invoke(animHashKey.Hit);
    }

    /// <summary>
    /// 로코모션 Bool 파라미터 전체 초기화
    /// </summary>
    private void ResetLocomotionBools()
    {
        animator.SetBool(animHashKey.Idle, false);
        animator.SetBool(animHashKey.Walk, false);
        animator.SetBool(animHashKey.Run, false);
        animator.SetBool(animHashKey.Jump, false);
    }
}

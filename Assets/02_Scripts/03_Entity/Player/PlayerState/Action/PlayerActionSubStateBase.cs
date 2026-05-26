using UnityEngine;

/// <summary>
/// 도구/상호작용 액션 하위 상태의 공통 베이스.
/// FemalePlayer 애니메이터는 트리거로 액션을 시작하고, 액션 애니메이션이 끝나면
/// PlayerActionState SM이 조건 없이 PlayerLocomotionState SM(기본 상태 Idle)으로 자동 복귀한다.
/// 스크립트는 이 흐름을 그대로 따라간다:
///   1) 애니메이터가 Idle을 벗어나 액션 애니메이션에 진입할 때까지 대기
///   2) 다시 Locomotion(Idle)으로 복귀하면 스크립트 상태도 Locomotion으로 전환
/// </summary>
public abstract class PlayerActionSubStateBase : PlayerSubStateBase
{
    // 애니메이터가 실제로 액션 애니메이션에 진입했는지 (진입 전 Idle 오인식 방지)
    private bool actionStarted;

    protected PlayerActionSubStateBase(PlayerRootStateMachine machine) : base(machine) { }

    /// <summary>이 액션이 재생할 트리거 파라미터 해시</summary>
    protected abstract int ActionTrigger { get; }

    /// <summary>액션 완료 시 장착 도구를 사용할지 여부 (벌목/채굴/땅파기 등)</summary>
    protected virtual bool UsesToolOnComplete => false;

    public override void OnEnter()
    {
        base.OnEnter();
        actionStarted = false;
        Machine.AnimData.PlayActionAnimation(ActionTrigger);
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(ActionTrigger);
    }

    public override void Update(float time = 1)
    {
        base.Update(time);

        bool inLocomotion = Machine.AnimData.IsInState(Machine.AnimData.AnimHashKey.Idle);

        // 1) 애니메이터가 액션 애니메이션에 진입(= Idle 이탈)할 때까지 대기
        if (!actionStarted)
        {
            if (!inLocomotion) actionStarted = true;
            return;
        }

        // 2) 액션 애니메이션 종료 → 애니메이터가 Locomotion(Idle)으로 복귀하면 스크립트도 전환
        if (inLocomotion)
        {
            if (UsesToolOnComplete) Entity.Inventory?.UseEquippedHandTool();
            GetRootState<PlayerActionState>()?.ChangeToLocomotion();
        }
    }
}

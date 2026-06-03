using UnityEngine;

/// <summary>
/// 도구/상호작용 액션 하위 상태의 공통 베이스.
/// 종료 판정을 애니메이터 상태 폴링(IsInState) 대신 Animation Event로 처리한다:
///   - OnEnter에서 액션 애니메이션 트리거
///   - 애니 클립의 효과(타격/완료) 프레임에 박힌 Animation Event가
///     PlayerAnimEventRelay → Player → PlayerActionState → 이 상태의 OnActionEvent()를 호출
///   - OnActionEvent에서 도구 데미지 적용 후, 반복 대상이 유효하면 다음 타격, 아니면 Locomotion 복귀
/// (PlayerActionState의 maxActionDuration이 이벤트 누락 시 안전망 역할)
/// </summary>
public abstract class PlayerActionSubStateBase : PlayerSubStateBase
{
    protected PlayerActionSubStateBase(PlayerRootStateMachine machine) : base(machine) { }

    /// <summary>이 액션이 재생할 트리거 파라미터 해시</summary>
    protected abstract int ActionTrigger { get; }

    /// <summary>액션 효과 시 장착 도구를 사용할지 여부 (벌목/채굴/땅파기 등)</summary>
    protected virtual bool UsesToolOnComplete => false;

    /// <summary>효과 발생 후 타겟이 유효하면 계속 반복할지 여부</summary>
    protected virtual bool ShouldLoop => false;

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(ActionTrigger);
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(ActionTrigger);
    }

    /// <summary>
    /// 액션 애니메이션의 효과 프레임 Animation Event에서 호출된다.
    /// 도구 데미지 적용 → 반복 대상이 유효하면 다음 타격, 아니면 Locomotion으로 복귀.
    /// </summary>
    public virtual void OnActionEvent()
    {
        if (UsesToolOnComplete)
            Entity.Inventory?.UseEquippedHandTool();

        // 루프 대상이고 타겟이 아직 유효하면 다음 타격 재생
        if (ShouldLoop
            && Entity.Inventory != null
            && Entity.Inventory.TryGetToolActionType(out _))
        {
            Machine.AnimData.SetRootState(Machine.AnimData.AnimHashKey.Action);
            Machine.AnimData.PlayActionAnimation(ActionTrigger);
            return;
        }

        GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

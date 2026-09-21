using UnityEngine;

/// <summary>
/// 도구/상호작용 액션 하위 상태의 공통 베이스.
///
/// 타격 판정과 1인칭 도구 스윙을 바디 애니메이션의 실제 타격 프레임에 맞춘다:
///   - OnEnter에서 액션 애니메이션 트리거 + 도구 스윙 연출 시작 + 타격 타이머 시작
///   - SwingRiseDuration + SwingStrikeDuration 이 지난 순간(= 바디가 내려찍는 프레임)에 도구 데미지 적용
///   - ActionDuration(바디 클립 Begin+Loop+Stop 합)이 지나면 반복 또는 Locomotion 복귀
///
/// 각 구간 길이는 애니메이터의 클립에서 실측한 값을 파생 클래스가 지정한다.
/// Dig/Build 클립처럼 Animation Event가 박혀 있지 않은 액션도 있으므로 종료 판정은 타이머가 담당하고,
/// Chop/Mine 클립의 OnActionStop 이벤트는 같은 시점에 들어오는 보조 신호로만 쓴다.
/// (PlayerActionState의 maxActionDuration이 그 둘 다 실패했을 때의 안전망)
/// </summary>
public abstract class PlayerActionSubStateBase : PlayerSubStateBase
{
    protected PlayerActionSubStateBase(PlayerRootStateMachine machine) : base(machine) { }

    /// <summary>이 액션이 재생할 트리거 파라미터 해시</summary>
    protected abstract int ActionTrigger { get; }

    /// <summary>
    /// 반복 재생 시 되돌아갈 애니메이터 상태 해시 (Action_Chop_Begin 등).
    /// 애니메이터의 Stop 상태는 Action bool이 false일 때만 Exit로 빠지므로,
    /// 반복하려면 Entry를 거치지 않고 Begin 상태로 직접 CrossFade해야 한다.
    /// ShouldLoop를 켜는 상태는 반드시 이 값을 지정하고, PlayerAnimData.s_crossFadeStates에도 추가할 것.
    /// </summary>
    protected virtual int LoopStateHash => 0;

    /// <summary>액션 효과 시 장착 도구를 사용할지 여부 (벌목/채굴/땅파기/철거 등)</summary>
    protected virtual bool UsesToolOnComplete => false;

    /// <summary>도구 사용 키를 누르고 있는 동안 타격을 반복할지 여부</summary>
    protected virtual bool ShouldLoop => false;

    #region 연출 구간 (바디 애니메이션 클립에서 실측한 값)

    /// <summary>도구를 최고점까지 들어올리는 시간 (Begin 클립 전체 + Loop 클립의 최고점까지)</summary>
    protected virtual float SwingRiseDuration => 0f;

    /// <summary>최고점에서 타격 지점까지 내려찍는 시간</summary>
    protected virtual float SwingStrikeDuration => 0f;

    /// <summary>타격 후 원위치로 돌아오는 시간 (Stop 클립이 끝날 때까지)</summary>
    protected virtual float SwingRecoverDuration => 0f;

    /// <summary>액션 시작부터 바디가 실제로 내려찍는 순간까지의 시간</summary>
    protected float HitTime => SwingRiseDuration + SwingStrikeDuration;

    /// <summary>바디 애니메이션 전체 길이. 0이면 타이머로 종료하지 않고 Animation Event를 기다린다.</summary>
    protected float ActionDuration => SwingRiseDuration + SwingStrikeDuration + SwingRecoverDuration;

    #endregion

    private float elapsedTime;
    private bool hitApplied;
    private bool finished;

    public override void OnEnter()
    {
        base.OnEnter();
        Machine.AnimData.PlayActionAnimation(ActionTrigger);
        StartSwing();
    }

    public override void OnExit()
    {
        base.OnExit();
        Machine.AnimData.ResetActionTrigger(ActionTrigger);
    }

    public override void Update(float deltaTime = 1f)
    {
        if (finished) return;

        elapsedTime += deltaTime;

        if (!hitApplied && elapsedTime >= HitTime)
            ApplyHit();

        // 연출 길이를 지정하지 않은 액션(Pick/Cook 등)은 Animation Event로만 끝난다.
        if (ActionDuration > 0f && elapsedTime >= ActionDuration)
            Finish();
    }

    /// <summary>
    /// 액션 클립의 스윙 시작 프레임 Animation Event에서 호출된다(있는 클립만).
    /// OnEnter와 실제 클립 시작 사이의 한 프레임 오차를 보정하는 용도다.
    /// </summary>
    public virtual void OnSwingEvent()
    {
        if (finished || hitApplied) return;
        elapsedTime = 0f;
    }

    /// <summary>
    /// 액션 클립의 종료 프레임 Animation Event에서 호출된다(있는 클립만).
    /// 타이머가 먼저 끝냈다면 무시된다.
    /// </summary>
    public virtual void OnActionEvent()
    {
        Finish();
    }

    // 타격 타이머와 1인칭 도구 스윙을 같은 기준점에서 출발시킨다.
    private void StartSwing()
    {
        elapsedTime = 0f;
        hitApplied = !UsesToolOnComplete; // 도구를 쓰지 않는 액션은 타격 판정이 없다
        finished = false;

        if (SwingRiseDuration > 0f)
        {
            Entity.FPCameraController?.PlayToolSwing(
                SwingRiseDuration,
                SwingStrikeDuration,
                SwingRecoverDuration);
        }
    }

    private void ApplyHit()
    {
        hitApplied = true;
        Entity.Inventory?.UseEquippedHandTool();
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        // 타격 타이밍을 놓친 채 클립이 끝났다면 여기서 보정한다.
        if (!hitApplied)
            ApplyHit();

        // 도구 사용 키를 누르고 있고 타겟이 아직 유효하면 다음 타격을 재생한다.
        // (키를 떼거나 시선을 돌려 타겟이 사라지면 Locomotion으로 복귀)
        if (ShouldLoop
            && LoopStateHash != 0
            && Input != null
            && Input.ToolUseHeld
            && Entity.Inventory != null
            && Entity.Inventory.TryGetToolActionType(out _))
        {
            Machine.AnimData.PlayCrossFade(LoopStateHash);
            StartSwing();
            GetRootState<PlayerActionState>()?.ResetTimeout();
            return;
        }

        GetRootState<PlayerActionState>()?.ChangeToLocomotion();
    }
}

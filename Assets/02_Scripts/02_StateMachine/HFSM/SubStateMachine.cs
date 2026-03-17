using System;

/// <summary>
/// HFSM 하위 상태 머신
/// RootStateBase가 내부적으로 소유하며, 하위 상태들을 관리
/// </summary>
public class SubStateMachine<TEntity> : StateMachine<SubStateBase<TEntity>> where TEntity : class
{
    public Action ExitCurState => ExitCurrentState;
    protected override void ExitCurrentState()
    {
        CurrentState?.OnExit();
        CurrentState = null;
    }
}
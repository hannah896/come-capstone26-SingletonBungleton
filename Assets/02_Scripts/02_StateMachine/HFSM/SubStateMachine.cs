using System;
using UnityEngine;

public class SubStateMachine : StateMachine<SubStateBase>
{
    protected RootStateBase rootStateBase;
    public override SubStateBase CurrentState { get => base.CurrentState; protected set => base.CurrentState = value; }

    public Action Exit;

    public SubStateMachine(RootStateBase rootStateBase)
    {
        this.rootStateBase = rootStateBase;
    }

    public override void ChangeState(SubStateBase Nextstate)
    {
        base.ChangeState(Nextstate);
    }

    public override void Init(SubStateBase state)
    {
        base.Init(state);
    }
}

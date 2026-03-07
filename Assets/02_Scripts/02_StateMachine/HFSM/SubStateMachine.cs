using System;
using UnityEngine;

public class SubStateMachine : StateMachine<SubStateBase>
{
    protected RootStateBase rootStateBase;
    public override SubStateBase CurrentState { get => base.CurrentState; protected set => base.CurrentState = value; }

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

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
    }

    public override void OnGameUpdate(float deltaTime)
    {
        base.OnGameUpdate(deltaTime);
    }

    protected override void EnterNextState(SubStateBase Nextstate)
    {
        base.EnterNextState(Nextstate);
    }

    protected override void ExitCurrentState()
    {
        base.ExitCurrentState();
    }
}

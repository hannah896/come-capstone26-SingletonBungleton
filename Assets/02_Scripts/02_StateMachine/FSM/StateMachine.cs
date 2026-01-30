using System;
using UnityEngine;

public class StateMachine<T> where T : StateBase
{
    protected T currentState;
    public virtual T CurrentState { get => currentState; protected set => currentState = value; }

    public Action Update;
    public Action FixedUpdate;

    public virtual void Init(T state)
    { 
        CurrentState = state;
        CurrentState.OnEnter();

        FixedUpdate += CurrentState.FixedUpdate;
        Update += CurrentState.Update;
    }

    public virtual void ChangeState(T Nextstate)
    {
        ExitCurrentState();
        Debug.Log($"State Change : {CurrentState?.GetType().Name} -> {Nextstate?.GetType().Name}");
        EnterNextState(Nextstate);
    }

    protected void ExitCurrentState()
    {
        CurrentState?.OnExit();
        Update -= CurrentState.Update;
        FixedUpdate -= CurrentState.FixedUpdate;
    }

    protected void EnterNextState(T Nextstate)
    {
        CurrentState = Nextstate;
        CurrentState?.OnEnter();
        Update += CurrentState.Update;
        FixedUpdate += CurrentState.FixedUpdate;
    }
}
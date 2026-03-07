using System;
using UnityEngine;

public class StateMachine<T> where T : StateBase
{
    protected T currentState;
    public virtual T CurrentState { get => currentState; protected set => currentState = value; }

    public virtual void Init(T state)
    { 
        CurrentState = state;
        CurrentState.OnEnter();
    }

    public virtual void ChangeState(T Nextstate)
    {
        ExitCurrentState();
        Debug.Log($"State Change : {CurrentState?.GetType().Name} -> {Nextstate?.GetType().Name}");
        EnterNextState(Nextstate);
    }

    // LoopManager와 호환되는 메서드
    public virtual void OnUpdate(float deltaTime)
    {
        CurrentState?.Update();
    }

    public virtual void OnGameUpdate(float deltaTime)
    {
        CurrentState?.FixedUpdate();
    }

    protected virtual void EnterNextState(T Nextstate)
    {
        CurrentState = Nextstate;
        CurrentState?.OnEnter();
    }

    protected virtual void ExitCurrentState()
    {
        CurrentState?.OnExit();
    }
}
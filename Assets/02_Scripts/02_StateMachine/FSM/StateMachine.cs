using System;

public class StateMachine<T> where T : StateBase
{
    protected static T currentState;
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
        Debug.Log($"State Change : {CurrentState?.GetType().Name} -> {Nextstate?.GetType().Name}");
        CurrentState?.OnExit();
        Update -= CurrentState.Update;
        FixedUpdate -= CurrentState.FixedUpdate;

        CurrentState = Nextstate;
        CurrentState?.OnEnter();
        Update += CurrentState.Update;
        FixedUpdate += CurrentState.FixedUpdate;
    }
}
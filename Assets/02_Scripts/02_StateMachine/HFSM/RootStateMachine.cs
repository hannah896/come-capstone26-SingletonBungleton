using UnityEngine;

public class RootStateMachine : StateMachine<StateBase>
{
    public SubStateMachine SubStateMachine { get; private set; }
    public override StateBase CurrentState { get => base.CurrentState; protected set => base.CurrentState = value; }

    public Entity Entity;
    public override void ChangeState(StateBase Nextstate)
    {
        base.ChangeState(Nextstate);
    }

    public override void Init(StateBase state)
    {
        base.Init(state);
    }
}

public abstract class StateBase
{
    protected StateMachine<StateBase> stateMachine;

    protected StateBase currentState => stateMachine.CurrentState;


    public StateBase(StateMachine<StateBase> stateMachine)
    {
        this.stateMachine = stateMachine;
    }


    public abstract void OnEnter();

    public abstract void OnExit();

    public abstract void FixedUpdate();

    public abstract void Update();
}
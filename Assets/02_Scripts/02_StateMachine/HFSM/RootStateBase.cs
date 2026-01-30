using UnityEngine;

public class RootStateBase : StateBase
{
    protected SubStateMachine subStateMachine;
    protected SubStateMachine SubStateMachine => subStateMachine;


    public RootStateBase(StateMachine<StateBase> stateMachine) : base(stateMachine)
    {
        subStateMachine = new SubStateMachine(this);
    }

    public override void OnEnter()
    {

    }

    public override void OnExit()
    {
        subStateMachine.Exit?.Invoke();
    }

    public override void FixedUpdate()
    {

    }

    public override void Update()
    {

    }
}

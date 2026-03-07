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
        Main.Loop.OnGameUpdate += Update;
    }

    public override void OnExit()
    {
        subStateMachine.Exit?.Invoke();
        Main.Loop.OnGameUpdate -= Update;
    }

    public override void FixedUpdate(float time = 1)
    {

    }

    public override void Update(float time = 1)
    {

    }
}

using UnityEngine;

public class SubStateBase : StateBase
{
    public SubStateBase(StateMachine<StateBase> stateMachine) : base(stateMachine) { }
    public override void OnEnter()
    {
        Main.Loop.OnGameUpdate += Update;
    }

    public override void OnExit()
    {
        Main.Loop.OnGameUpdate -= Update;
    }

    public override void Update(float time = 1)
    {

    }

    public override void FixedUpdate(float time = 1)
    {

    }
}
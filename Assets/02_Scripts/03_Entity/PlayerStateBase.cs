using UnityEngine;

public class PlayerStateBase : StateBase
{
    public PlayerStateBase(StateMachine<StateBase> stateMachine) : base(stateMachine)
    {
    }

    public override void OnEnter()
    {
        throw new System.NotImplementedException();
    }

    public override void OnExit()
    {
        throw new System.NotImplementedException();
    }

    public override void FixedUpdate()
    {
        throw new System.NotImplementedException();
    }

    public override void Update()
    {
        throw new System.NotImplementedException();
    }

}

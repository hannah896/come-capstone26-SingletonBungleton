using UnityEngine;

public class PlayerStateMachine : StateMachine<PlayerStateBase>
{
    public override PlayerStateBase CurrentState { get => base.CurrentState; protected set => base.CurrentState = value; }

    public override void Init(PlayerStateBase state)
    {
        base.Init(state);
    }


    public override void ChangeState(PlayerStateBase Nextstate)
    {
        base.ChangeState(Nextstate);
    }
}
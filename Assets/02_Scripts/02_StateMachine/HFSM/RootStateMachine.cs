using UnityEngine;

public class RootStateMachine<R, S> : StateMachine<R> where R : RootStateBase where S : SubStateBase
{
    public S SubStateMachine { get; private set; }

    public override void ChangeState(R Nextstate)
    {
        base.ChangeState(Nextstate);
    }

    public override void Init(R state)
    {
        base.Init(state);
    }
}
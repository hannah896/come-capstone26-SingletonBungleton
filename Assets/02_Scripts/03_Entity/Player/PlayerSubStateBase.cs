using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class PlayerSubStateBase : SubStateBase
{
    public PlayerSubStateBase(StateMachine<StateBase> stateMachine) : base(stateMachine)
    {
    }

    protected override StateBase currentState => base.currentState;

    public override void FixedUpdate(float time = 1)
    {
        base.FixedUpdate(time);
    }

    public override void OnEnter()
    {
        base.OnEnter();
    }

    public override void OnExit()
    {
        base.OnExit();
    }

    public override void Update(float time = 1)
    {
        base.Update(time);
    }
}
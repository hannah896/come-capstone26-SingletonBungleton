using UnityEngine;

public class PlayerRootStateMachine : RootStateMachine<PlayerRootStateBase, PlayerSubStateBase>
{
    private PlayerAnimData animData;
    private Player player;

    #region Properties
    public override PlayerRootStateBase CurrentState { get => currentState; protected set => currentState = value; }
    public Player Player => player;
    public PlayerAnimData AnimData => animData;
    #endregion

    public PlayerRootStateMachine(Player player, Animator animator)
    {
        this.player = player;
        this.animData = new PlayerAnimData(animator);
    }

    public override void Init(PlayerRootStateBase state)
    { 
        CurrentState = state;
        CurrentState.OnEnter();
    }

    protected override void EnterNextState(PlayerRootStateBase Nextstate)
    {
        base.EnterNextState(Nextstate);
    }

    protected override void ExitCurrentState()
    {
        base.ExitCurrentState();
    }
}
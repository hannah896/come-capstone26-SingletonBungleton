using UnityEngine;

public class PlayerRootStateMachine : RootStateMachine<Player, PlayerRootStateBase>
{
    public PlayerAnimData AnimData { get; private set; }
    public string CurrentStateName => CurrentState?.GetType().Name ?? "None";
    public string CurrentSubStateName => CurrentState?.CurrentSubStateName ?? "None";

    public PlayerRootStateMachine(Player player, Animator animator) : base(player)
    {
        AnimData = new PlayerAnimData(animator);
    }
}

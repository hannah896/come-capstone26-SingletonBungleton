using UnityEngine;

public class PlayerRootStateMachine : RootStateMachine<Player, PlayerRootStateBase>
{
    public PlayerAnimData AnimData { get; private set; }

    public PlayerRootStateMachine(Player player, Animator animator) : base(player)
    {
        AnimData = new PlayerAnimData(animator);
    }
}

using UnityEngine;

public class PlayerAnimData
{
    #region field
    private Animator animator;
    private PlayerAnimHashKey animHashKey;
    #endregion

    #region Properties
    public PlayerRootStateBase IdleState { get; private set; }
    public PlayerRootStateBase MoveState { get; private set; }
    public PlayerRootStateBase TraceState { get; private set; }
    public PlayerRootStateBase RunState { get; private set; }

    #endregion

    public PlayerAnimHashKey AnimHashKey => animHashKey;

    public PlayerAnimData(Animator animator)
    {
        animHashKey = new();
        this.animator = animator;
        this.animHashKey = new PlayerAnimHashKey();
    }
}
using UnityEngine;

/// <summary>
/// 플레이어 상태의 기본 클래스
/// </summary>
public abstract class PlayerRootStateBase: RootStateBase
{
    protected Player player;
    protected PlayerRootStateMachine machine;

    protected PlayerRootStateBase(StateMachine<StateBase> machine) : base(machine)
    {
        this.player = this.machine?.Player;
    }

    public virtual void FixedUpdate()
    {
    }

    public virtual void Update()
    {
    }
}

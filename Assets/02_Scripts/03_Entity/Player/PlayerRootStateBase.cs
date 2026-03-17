using UnityEngine;

/// <summary>
/// 플레이어 루트 상태의 기본 클래스
/// </summary>
public abstract class PlayerRootStateBase : RootStateBase<Player>
{
    protected PlayerRootStateMachine Machine { get; private set; }
    protected PlayerInputData Input => Entity.InputData;

    protected PlayerRootStateBase(PlayerRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }
}

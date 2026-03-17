using UnityEngine;

/// <summary>
/// 플레이어 하위 상태의 기본 클래스
/// </summary>
public abstract class PlayerSubStateBase : SubStateBase<Player>
{
    protected PlayerRootStateMachine Machine { get; private set; }
    protected PlayerInputData Input => Entity.InputData;

    protected PlayerSubStateBase(PlayerRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }

    /// <summary>
    /// 현재 Root 상태를 특정 타입으로 캐스팅하여 가져온다.
    /// </summary>
    protected T GetRootState<T>() where T : PlayerRootStateBase
    {
        return Machine.CurrentState as T;
    }
}

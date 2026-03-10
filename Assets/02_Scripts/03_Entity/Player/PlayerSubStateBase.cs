/// <summary>
/// 플레이어 하위 상태의 기본 클래스
/// </summary>
public abstract class PlayerSubStateBase : SubStateBase<Player>
{
    protected PlayerRootStateMachine Machine { get; private set; }

    protected PlayerSubStateBase(PlayerRootStateMachine machine) : base(machine.Owner)
    {
        Machine = machine;
    }
}

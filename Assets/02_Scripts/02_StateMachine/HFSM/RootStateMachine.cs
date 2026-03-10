/// <summary>
/// HFSM 루트 상태 머신
/// TEntity: 소유 엔티티, TRoot: 루트 상태 타입
/// </summary>
public class RootStateMachine<TEntity, TRoot> : StateMachine<TRoot>
    where TEntity : class
    where TRoot : RootStateBase<TEntity>
{
    public TEntity Owner { get; private set; }

    public RootStateMachine(TEntity owner)
    {
        Owner = owner;
    }
}

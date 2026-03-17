/// <summary>
/// HFSM 루트 상태 기본 클래스
/// 내부에 SubStateMachine을 소유하여 하위 상태를 관리
/// </summary>
public abstract class RootStateBase<TEntity> : StateBase where TEntity : class
{
    protected TEntity Entity { get; private set; }
    protected SubStateMachine<TEntity> SubStateMachine { get; private set; }

    protected RootStateBase(TEntity entity)
    {
        Entity = entity;
        SubStateMachine = new SubStateMachine<TEntity>();
    }

    public override void OnEnter() { }

    public override void OnExit()
    {
        SubStateMachine?.ExitCurState?.Invoke();
    }

    // 루트 상태의 Update/FixedUpdate가 하위 상태 머신도 구동
    public override void Update(float time = 1.0f)
    {
        SubStateMachine.OnUpdate(time);
    }

    public override void FixedUpdate(float time = 1.0f)
    {
        SubStateMachine.OnGameUpdate(time);
    }
}

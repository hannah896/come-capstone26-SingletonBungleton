/// <summary>
/// HFSM 하위 상태 기본 클래스
/// TEntity: 이 상태를 소유하는 엔티티 타입
/// </summary>
public abstract class SubStateBase<TEntity> : StateBase where TEntity : class
{
    protected TEntity Entity { get; private set; }

    protected SubStateBase(TEntity entity)
    {
        Entity = entity;
    }

    public override void OnEnter() { }
    public override void OnExit() { }
    public override void Update(float time = 1.0f) { }
    public override void FixedUpdate(float time = 1.0f) { }
}

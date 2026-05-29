/// <summary>
/// 비적대 Mob(소·닭·오리 등). 평소 배회하다 위협 시 도망친다. 상태 관리는 AnimalStateMachine이 전담.
/// </summary>
public class Animal : Mob
{
    private AnimalStateMachine stateMachine;

    /// <summary>FleeRange 등 동물 전용 스탯 접근용.</summary>
    protected AnimalStatData AnimalData => statData as AnimalStatData;

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new AnimalStateMachine(this);
    }

    protected override void OnGameUpdate(float deltaTime)
        => stateMachine.OnUpdate(deltaTime);

    protected override void OnDeath()
        => stateMachine.ToDead();
}

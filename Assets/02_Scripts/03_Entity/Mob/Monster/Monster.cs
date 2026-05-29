/// <summary>
/// 적대 Mob. 플레이어를 인지·추적·공격한다. 상태 관리는 MonsterStateMachine이 전담.
/// </summary>
public class Monster : Mob
{
    private MonsterStateMachine stateMachine;

    /// <summary>FOV·DetectRange 등 몬스터 전용 스탯 접근용.</summary>
    protected MonsterStatData MonsterData => statData as MonsterStatData;

    protected override void Awake()
    {
        base.Awake();
        stateMachine = new MonsterStateMachine(this);
    }

    protected override void OnGameUpdate(float deltaTime)
        => stateMachine.OnUpdate(deltaTime);

    protected override void OnDeath()
        => stateMachine.ToDead();
}

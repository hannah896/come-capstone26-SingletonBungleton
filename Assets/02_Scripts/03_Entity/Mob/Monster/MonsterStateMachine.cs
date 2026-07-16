/// <summary>
/// Monster 전용 상태 머신. 상태 생성·전환 로직을 Monster 본체에서 분리해 이 클래스가 전담한다.
/// 종류별 몬스터는 이 클래스를 상속해 To* 전환 메서드를 오버라이드하면 전용 상태로 교체할 수 있다.
/// </summary>
public class MonsterStateMachine : StateMachine<MobState<Monster>>
{
    protected readonly Monster owner;

    public MonsterStateMachine(Monster owner)
    {
        this.owner = owner;
        Init(new MonsterSpawnState(owner, this)); // 초기 상태: 등장 연출 → (Animation Event) → Idle
    }

    public virtual void ToSpawn()
        => ChangeState(new MonsterSpawnState(owner, this));

    public virtual void ToIdle()
        => ChangeState(new MonsterIdleState(owner, this));

    public virtual void ToChase()
        => ChangeState(new MonsterChaseState(owner, this));

    public virtual void ToAttack()
        => ChangeState(new MonsterAttackState(owner, this));

    public virtual void ToHit()
        => ChangeState(new MonsterHitState(owner, this));

    public virtual void ToDead()
        => ChangeState(new MobDeadState<Monster>(owner, this));
}

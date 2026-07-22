/// <summary>
/// 스폰(등장) 상태. 등장 연출이 나오는 동안 타깃 탐색/이동을 하지 않고 기다리기만 한다.
/// 등장 클립은 컨트롤러의 기본(Default) 상태라 자동으로 재생되므로, 여기서는 Bool을 모두 끄기만 한다.
/// 연출이 끝나는 시점은 클립 마지막에 심은 Animation Event(Monster.OnSpawnAnimEnd)가 알려주고, 그때 Idle로 넘어간다.
/// 등장 연출을 쓰지 않거나(UseSpawnAnim=false) 이벤트가 끝내 오지 않으면 Idle로 넘긴다.
/// </summary>
public class MonsterSpawnState : MobState<Monster>
{
    private readonly MonsterStateMachine sm;
    private float fallbackRemaining;

    public MonsterSpawnState(Monster owner, MonsterStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        // Bool이 하나라도 켜져 있으면 Entry가 그쪽으로 분기해 등장 연출이 씹힌다.
        Owner.ClearAnimBools();

        // 등장 연출을 쓰지 않으면 대기할 이유가 없다.
        if (!Owner.UseSpawnAnim)
        {
            sm.ToIdle();
            return;
        }

        fallbackRemaining = Owner.SpawnFallbackTimeout;
    }

    public override void Update(float time = 1.0f)
    {
        // 정상 흐름은 OnSpawnAnimEnd(Animation Event)가 끝낸다. 여기서는 그게 안 왔을 때만 걷어낸다.
        fallbackRemaining -= time;
        if (fallbackRemaining <= 0f)
            sm.ToIdle();
    }
}

/// <summary>
/// 트리가드 그로기 상태. 강인도가 깨지면 하던 기술을 끊고 들어온다.
/// Take Damage 클립을 한 번 재생한 뒤 Idle로 서서 GroggyDuration 동안 무방비로 있는다(받는 데미지 증가).
/// 끝나면 강인도를 가득 채우고 다시 교전한다.
/// </summary>
public class TreeguardGroggyState : MobState<Monster>
{
    private readonly TreeguardStateMachine sm;
    private readonly Treeguard boss;
    private float elapsed;
    private bool idleSet;

    public TreeguardGroggyState(Treeguard owner, TreeguardStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    // 그로기 중에도 피격 경직으로 넘어가지 않는다. (ToHit은 어차피 비어 있지만 의도를 분명히 한다)
    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        idleSet = false;
        boss.SetGroggy(true);
        boss.PlayAnim(MonsterAnimId.Hit);
    }

    public override void OnExit()
    {
        // 그로기 중 사망 등으로 끊겨도 플래그가 남지 않게 한다.
        boss.SetGroggy(false);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;

        if (!idleSet && elapsed >= boss.GroggyClipLength)
        {
            idleSet = true;
            boss.PlayAnim(MonsterAnimId.Idle);
        }

        if (elapsed < boss.GroggyDuration) return;

        sm.ToAttack();
    }
}

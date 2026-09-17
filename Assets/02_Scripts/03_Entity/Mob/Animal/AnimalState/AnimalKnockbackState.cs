/// <summary>
/// 넉백 상태. 피격 시 진입해 Rigidbody로 공격자 반대 방향 위로 튕겨 오르고,
/// 땅에 다시 닿으면 도망(Flee)으로 전환한다. 착지를 못 하면 MaxAirTime 후 강제로 넘어간다.
/// </summary>
public class AnimalKnockbackState : MobState<Animal>
{
    private readonly AnimalStateMachine sm;
    private float elapsed;

    public AnimalKnockbackState(Animal owner, AnimalStateMachine machine) : base(owner, machine)
    {
        sm = machine;
    }

    public override void OnEnter()
    {
        elapsed = 0f;
        Owner.PlayAnim(AnimalAnimId.Idle);
        Owner.BeginKnockback();
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;
        if (elapsed < Owner.MinAirTime) return;

        if (Owner.IsGrounded() || elapsed >= Owner.MaxAirTime)
            sm.ToFlee();
    }

    // 착지/사망/재피격 등 어떤 이유로 나가든 물리를 멈추고 Agent로 복귀시킨다.
    public override void OnExit()
        => Owner.EndKnockback();
}

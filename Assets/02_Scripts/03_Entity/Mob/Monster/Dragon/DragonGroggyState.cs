/// <summary>
/// 드래곤 그로기. 무력화 파괴(GetHit1 반복) · 기둥 충돌 · 격추(땅에 처박힌 자세 유지)에서 들어온다.
/// 지속 시간 동안 무방비로 받는 피해가 늘고, 끝나면 무력화 게이지를 가득 채운 뒤 다시 교전한다.
/// 격추(Crash)는 마지막 1초 동안 몸부림(GetHit1)을 보여 주고 일어난다.
/// </summary>
public class DragonGroggyState : MobState<Monster>
{
    private const float RecoverTime = 1f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private readonly float duration;
    private readonly float damageMultiplier;
    private readonly MonsterAnimId holdAnim;
    private float elapsed;
    private bool recovering;

    public DragonGroggyState(DragonBoss owner, DragonStateMachine machine, float duration, float damageMultiplier,
                             MonsterAnimId holdAnim) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
        this.duration = duration;
        this.damageMultiplier = damageMultiplier;
        this.holdAnim = holdAnim;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        elapsed = 0f;
        recovering = false;
        boss.SetCounterWindow(false);
        boss.SetGroggy(true, damageMultiplier);
        boss.PlayAnim(holdAnim);
    }

    public override void OnExit()
    {
        // 그로기 중 사망 등으로 끊겨도 플래그가 남지 않게 한다.
        boss.SetGroggy(false);
    }

    public override void Update(float time = 1.0f)
    {
        elapsed += time;

        if (!recovering && holdAnim == MonsterAnimId.Crash && elapsed >= duration - RecoverTime)
        {
            recovering = true;
            boss.PlayAnim(MonsterAnimId.Hit);
        }

        if (elapsed < duration) return;

        boss.PlayAnim(MonsterAnimId.Idle);
        sm.ToAttack();
    }
}

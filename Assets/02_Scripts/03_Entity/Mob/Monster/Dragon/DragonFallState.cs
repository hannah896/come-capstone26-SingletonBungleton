using UnityEngine;

/// <summary>
/// 격추 성공: 추락 → 땅에 처박힘 → 그로기.
///
/// 제자리 클립 특성상 FlyStationaryToFalling 동안 몸이 루트 기준으로 ~5m 솟아오르므로,
/// 그만큼 루트를 내려 화면상 몸 높이를 유지한다. 이어서 중력처럼 가속하며 루트를 땅까지 떨어뜨리고,
/// 땅에 닿으면 DeathHitTheGround(몸이 바닥에 처박히는 클립)를 틀고 주변을 밀어낸 뒤 그로기로 넘어간다.
/// </summary>
public class DragonFallState : MobState<Monster>
{
    private enum Step { Tilt, Drop, Crash }

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private Step step;
    private float elapsed;
    private float startAltitude;
    private float velocity;
    private bool impactDone;

    public DragonFallState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        step = Step.Tilt;
        elapsed = 0f;
        velocity = 0f;
        impactDone = false;
        startAltitude = boss.Altitude;
        boss.PlayAnim(MonsterAnimId.Fall);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null)
        {
            boss.EndAirPhase();
            sm.ToAttack();
            return;
        }

        elapsed += time;

        switch (step)
        {
            case Step.Tilt:
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(d.FallStartDuration, 0.1f));
                boss.SetAltitude(Mathf.Max(startAltitude - d.FallStartDrop * t, 0f));
                if (t >= 1f)
                {
                    step = Step.Drop;
                    elapsed = 0f;
                }
                break;
            }

            case Step.Drop:
            {
                velocity += d.FallGravity * time;
                float alt = boss.Altitude - velocity * time;
                if (alt > 0f)
                {
                    boss.SetAltitude(alt);
                    break;
                }

                boss.EndAirPhase(); // 땅에 닿았다: 고도 0, 무력화 게이지로 복귀
                boss.PlayAnim(MonsterAnimId.Crash);
                step = Step.Crash;
                elapsed = 0f;
                break;
            }

            case Step.Crash:
                if (!impactDone && elapsed >= d.CrashImpactTime)
                {
                    impactDone = true;
                    boss.CrashImpact();
                }

                if (elapsed >= d.CrashDuration)
                    sm.ToGroggy(d.ShootdownGroggyDuration, d.ShootdownDamageMultiplier, MonsterAnimId.Crash);
                break;
        }
    }
}

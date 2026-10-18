using UnityEngine;

/// <summary>
/// 이륙. TakeOff 클립(제자리)에 맞춰 루트를 체공 고도까지 올린다. 끝나면 체공(DragonAirState).
/// 이 순간부터 무력화 게이지가 격추 게이지로 바뀐다.
/// </summary>
public class DragonTakeOffState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private bool clipStarted;
    private float waitTimer;
    private float elapsed;

    public DragonTakeOffState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        clipStarted = false;
        waitTimer = MaxClipWait;
        elapsed = 0f;
        boss.SetCounterWindow(false);
        boss.BeginAirPhase();
        boss.PlayAnim(MonsterAnimId.TakeOff);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null)
        {
            sm.ToAir();
            return;
        }

        if (!clipStarted)
        {
            waitTimer -= time;
            if (boss.IsInClip(MonsterAnimId.TakeOff) || waitTimer <= 0f)
                clipStarted = true;
            return;
        }

        elapsed += time;
        float t = Mathf.Clamp01((elapsed - d.TakeOffRiseStart) / Mathf.Max(d.TakeOffDuration - d.TakeOffRiseStart, 0.1f));
        boss.SetAltitude(Mathf.SmoothStep(0f, d.FlyHeight, t));

        if (elapsed < d.TakeOffDuration) return;
        sm.ToAir();
    }
}

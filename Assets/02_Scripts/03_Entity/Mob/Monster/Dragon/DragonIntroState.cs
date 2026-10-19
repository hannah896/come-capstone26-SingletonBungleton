using UnityEngine;

/// <summary>
/// 페이즈 전환 등장 연출 (기획 4.3 ④). 서리룡이 쓰러진 자리에서 망령룡이 일어선다.
/// ① 이륙: TakeOff 클립에 맞춰 체공 고도까지 오른다
/// ② 포효: 체공(FlyStationary)하며 IntroHoverTime 동안 버틴다
/// ③ 착지: FlyStationaryToLanding에 맞춰 땅까지 내려오고, 닿는 순간 주변을 밀어낸다(피해 없음)
/// 연출 내내 무적이다. 끝나면 교전 상태로 들어간다.
/// </summary>
public class DragonIntroState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;

    private enum Step { TakeOff, Hover, Land }

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private Step step;
    private float elapsed;
    private bool clipStarted;
    private float waitTimer;

    public DragonIntroState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        boss.SetInvulnerable(true);
        Begin(Step.TakeOff, MonsterAnimId.TakeOff);
    }

    public override void OnExit()
    {
        boss.SetInvulnerable(false);
    }

    private void Begin(Step next, MonsterAnimId anim)
    {
        step = next;
        elapsed = 0f;
        clipStarted = false;
        waitTimer = MaxClipWait;
        boss.PlayAnim(anim);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null)
        {
            boss.SetAltitude(0f);
            sm.ToAttack();
            return;
        }

        // 가장 가까운 플레이어 쪽을 본다 (등장하며 노려보는 느낌)
        var players = boss.CollectPlayers();
        if (players.Count > 0)
            boss.TurnTowards(players[0].transform.position - boss.transform.position, time, 90f);

        switch (step)
        {
            case Step.TakeOff:
                if (!WaitClip(MonsterAnimId.TakeOff, time)) return;
                elapsed += time;
                float t = Mathf.Clamp01((elapsed - d.TakeOffRiseStart) / Mathf.Max(d.TakeOffDuration - d.TakeOffRiseStart, 0.1f));
                boss.SetAltitude(Mathf.SmoothStep(0f, d.FlyHeight, t));
                if (elapsed >= d.TakeOffDuration) Begin(Step.Hover, MonsterAnimId.FlyIdle);
                break;

            case Step.Hover:
                elapsed += time;
                if (elapsed >= d.IntroHoverTime) Begin(Step.Land, MonsterAnimId.Land);
                break;

            case Step.Land:
                if (!WaitClip(MonsterAnimId.Land, time)) return;
                elapsed += time;
                float l = Mathf.Clamp01(elapsed / Mathf.Max(d.LandDescendTime, 0.1f));
                boss.SetAltitude(Mathf.Lerp(d.FlyHeight, 0f, Mathf.SmoothStep(0f, 1f, l)));
                if (elapsed < d.LandDuration) return;

                boss.SetAltitude(0f);
                boss.IntroShockwave();
                boss.PlayAnim(MonsterAnimId.Idle);
                sm.ToAttack();
                break;
        }
    }

    private bool WaitClip(MonsterAnimId anim, float time)
    {
        if (clipStarted) return true;
        waitTimer -= time;
        if (boss.IsInClip(anim) || waitTimer <= 0f) clipStarted = true;
        return clipStarted;
    }
}

using UnityEngine;

/// <summary>
/// 월식의 응시 (기획 5.3, 2페이즈 시선 기믹).
/// ① 이륙: TakeOff 클립에 맞춰 체공 고도까지 오른다
/// ② 예고: 아레나 중앙 상공으로 옮겨 가며 "그것이 당신을 본다" 카운트다운 (GazeWarningTime)
/// ③ 판정: 카운트가 끝나는 순간 각 플레이어 카메라가 보스 눈을 보고 있었으면 공포 (Ego 감소 + 이동 불가)
/// ④ 착지: 잠깐 머문 뒤 땅으로 내려와 바로 산성 침으로 이어간다 (눈을 뗀 사이 발밑을 놓치게)
///
/// 격추 페이즈가 아니므로 격추 게이지로 바뀌지 않고, 체공 중에도 평소처럼 피해를 받는다.
/// </summary>
public class DragonGazeState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;
    private const float HoverTurnSpeed = 120f;

    private enum Step { TakeOff, Warn, After, Land }

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private Step step;
    private float elapsed;
    private bool clipStarted;
    private float waitTimer;

    public DragonGazeState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        boss.SetCounterWindow(false);
        Begin(Step.TakeOff, MonsterAnimId.TakeOff);
    }

    // 중간에 끊겨도(사망 등) 땅에 서 있게
    public override void OnExit() => boss.SetAltitude(0f);

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

        switch (step)
        {
            case Step.TakeOff:
                if (!WaitClip(MonsterAnimId.TakeOff, time)) return;
                elapsed += time;
                float t = Mathf.Clamp01((elapsed - d.TakeOffRiseStart) / Mathf.Max(d.TakeOffDuration - d.TakeOffRiseStart, 0.1f));
                boss.SetAltitude(Mathf.SmoothStep(0f, d.FlyHeight, t));
                if (elapsed < d.TakeOffDuration) return;

                Begin(Step.Warn, MonsterAnimId.FlyIdle);
                boss.BeginGazeWarning();
                break;

            case Step.Warn:
                elapsed += time;
                DriftToCenter(d, time);
                FacePlayers(time);
                if (elapsed < d.GazeWarningTime) return;

                boss.GazeJudge();
                step = Step.After;
                elapsed = 0f;
                break;

            case Step.After:
                elapsed += time;
                if (elapsed >= d.GazeAfterHover) Begin(Step.Land, MonsterAnimId.Land);
                break;

            case Step.Land:
                if (!WaitClip(MonsterAnimId.Land, time)) return;
                elapsed += time;
                float l = Mathf.Clamp01(elapsed / Mathf.Max(d.LandDescendTime, 0.1f));
                boss.SetAltitude(Mathf.Lerp(d.FlyHeight, 0f, Mathf.SmoothStep(0f, 1f, l)));
                if (elapsed < d.LandDuration) return;

                boss.SetAltitude(0f);
                boss.PlayAnim(MonsterAnimId.Idle);
                // 바로 산성 침 (가장 가까운 플레이어를 노린다)
                var players = boss.CollectPlayers();
                if (players.Count > 0) boss.SetTarget(players[0]);
                sm.ToSkill(DragonSkill.Spit);
                break;
        }
    }

    // 아레나 중앙 상공으로 천천히 옮겨 간다
    private void DriftToCenter(DragonStatData d, float time)
    {
        Vector3 to = boss.ArenaCenter - boss.transform.position;
        to.y = 0f;
        float move = d.GazeDriftSpeed * time;
        if (to.sqrMagnitude <= move * move) boss.MovePlanar(to);
        else boss.MovePlanar(to.normalized * move);
    }

    // 플레이어들의 무게중심 쪽을 내려다본다 (어디서 봐도 눈이 보이도록)
    private void FacePlayers(float time)
    {
        var players = boss.CollectPlayers();
        if (players.Count == 0) return;
        Vector3 sum = Vector3.zero;
        foreach (var p in players) sum += p.transform.position;
        boss.TurnTowards(sum / players.Count - boss.transform.position, time, HoverTurnSpeed);
    }

    private bool WaitClip(MonsterAnimId anim, float time)
    {
        if (clipStarted) return true;
        waitTimer -= time;
        if (boss.IsInClip(anim) || waitTimer <= 0f) clipStarted = true;
        return clipStarted;
    }
}

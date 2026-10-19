using UnityEngine;

/// <summary>
/// 공중 페이즈 체공 상태. 아레나 중앙 상공으로 천천히 옮겨 가며 공중 패턴 두 가지를 번갈아 쓴다.
/// - 빙탄 폭격: 플레이어마다 발밑에 예고 원 → 착탄. BombardCount번 반복
/// - 눈보라 브레스: 몸통을 크게 돌리며 아레나 절반을 쓸어낸다 (기둥 뒤 엄폐)
///
/// 체공 중 받은 피해는 격추 게이지를 깎는다(근접은 닿지 않으므로 사실상 활·투척).
/// - 제한 시간 안에 게이지를 다 깎으면 → 추락(DragonFallState)
/// - 시간이 다 되면 → 활공 선회 후 서리 폭발(DragonGlideState)
/// 격추는 패턴 도중에도 즉시 일어난다.
/// </summary>
public class DragonAirState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;
    private const float DriftSpeed = 3f;      // 중앙 상공으로 옮겨 가는 속도(m/s)
    private const float HoverTurnSpeed = 90f;

    private enum Pattern { Hover, Bombard, Breath }

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;

    private float timeLeft;
    private Pattern pattern;
    private Pattern lastPattern = Pattern.Breath; // 첫 패턴은 폭격부터
    private float gapTimer;
    private bool clipStarted;
    private float waitTimer;
    private float elapsed;
    private int shotIndex;
    private float tickTimer;
    private float sweepSign;
    private float sweptSoFar;

    public DragonAirState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        var d = boss.Data;
        timeLeft = d != null ? d.ShootdownTimeLimit : 25f;
        gapTimer = d != null ? d.AirSkillGap : 1.5f;
        pattern = Pattern.Hover;
        boss.PlayAnim(MonsterAnimId.FlyIdle);
        if (d != null) boss.SetAltitude(d.FlyHeight);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null) return;

        // 격추 성공: 하던 패턴을 끊고 바로 떨어진다
        if (boss.IsShootdownComplete)
        {
            sm.ToFall();
            return;
        }

        timeLeft -= time;
        if (timeLeft <= 0f && pattern == Pattern.Hover)
        {
            sm.ToGlide();
            return;
        }

        Drift(time);

        switch (pattern)
        {
            case Pattern.Hover:   TickHover(d, time); break;
            case Pattern.Bombard: TickBombard(d, time); break;
            case Pattern.Breath:  TickBreath(d, time); break;
        }
    }

    // 아레나 중앙 상공으로 천천히 옮겨 간다 (브레스 중에는 제자리)
    private void Drift(float time)
    {
        if (pattern == Pattern.Breath) return;
        Vector3 to = boss.ArenaCenter - boss.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1f) return;
        boss.MovePlanar(Vector3.ClampMagnitude(to, DriftSpeed * time));
    }

    private void TickHover(DragonStatData d, float time)
    {
        FaceNearest(time, HoverTurnSpeed);

        gapTimer -= time;
        if (gapTimer > 0f) return;

        // 남은 시간 안에 끝낼 수 있는 패턴만 시작한다 (끝나고 바로 시간 초과 판정)
        pattern = lastPattern == Pattern.Bombard ? Pattern.Breath : Pattern.Bombard;
        lastPattern = pattern;
        clipStarted = false;
        waitTimer = MaxClipWait;
        elapsed = 0f;
        shotIndex = 0;
        tickTimer = 0f;
        sweptSoFar = 0f;
        sweepSign = Random.value < 0.5f ? 1f : -1f;
        if (pattern == Pattern.Breath) boss.BeginBreath();
        boss.PlayAnim(pattern == Pattern.Bombard ? MonsterAnimId.FlyRangeAttack : MonsterAnimId.FlyBreath);
    }

    private bool WaitClip(MonsterAnimId anim, float time)
    {
        if (clipStarted) return true;
        waitTimer -= time;
        if (boss.IsInClip(anim) || waitTimer <= 0f) clipStarted = true;
        return clipStarted;
    }

    // 빙탄 폭격: 클립이 루프하므로 BombardInterval마다 살아 있는 플레이어 전원 발밑에 한 발씩
    private void TickBombard(DragonStatData d, float time)
    {
        FaceNearest(time, HoverTurnSpeed);
        if (!WaitClip(MonsterAnimId.FlyRangeAttack, time)) return;
        elapsed += time;

        float releaseAt = d.FlySpitReleaseTime + d.BombardInterval * shotIndex;
        if (shotIndex < d.BombardCount && elapsed >= releaseAt)
        {
            foreach (var player in boss.CollectPlayers())
                boss.FireFrostBall(player.transform.position, d.BombardFlightTime, bombard: true);
            shotIndex++;
        }

        if (elapsed >= d.BombardInterval * d.BombardCount) EndPattern(d);
    }

    // 눈보라 브레스: 숨 들이쉬는 동안 시작 방향으로 틀고, 뿜는 동안 반대쪽으로 크게 쓸어낸다
    private void TickBreath(DragonStatData d, float time)
    {
        if (!WaitClip(MonsterAnimId.FlyBreath, time)) return;
        elapsed += time;

        float half = d.FlyBreathSweepAngle * 0.5f;
        float window = Mathf.Max(d.FlyBreathEndTime - d.FlyBreathStartTime, 0.1f);

        if (elapsed < d.FlyBreathStartTime)
        {
            Player near = Nearest();
            if (near != null)
            {
                Vector3 to = near.transform.position - boss.transform.position;
                Vector3 start = Quaternion.Euler(0f, -half * sweepSign, 0f) * to;
                boss.TurnTowards(start, time, d.AimTurnSpeed);
            }
        }
        else if (elapsed <= d.FlyBreathEndTime)
        {
            float target = d.FlyBreathSweepAngle * Mathf.Clamp01((elapsed - d.FlyBreathStartTime) / window);
            boss.AddYaw((target - sweptSoFar) * sweepSign);
            sweptSoFar = target;

            tickTimer -= time;
            if (tickTimer <= 0f)
            {
                tickTimer = d.BreathTickInterval;
                boss.BreathTick(d.FlyBreathRange);
            }
        }

        if (elapsed >= d.FlyBreathDuration) EndPattern(d);
    }

    private void EndPattern(DragonStatData d)
    {
        pattern = Pattern.Hover;
        gapTimer = d.AirSkillGap;
        boss.PlayAnim(MonsterAnimId.FlyIdle);
    }

    private Player Nearest()
    {
        var players = boss.CollectPlayers();
        return players.Count > 0 ? players[0] : null;
    }

    private void FaceNearest(float time, float turnSpeed)
    {
        Player near = Nearest();
        if (near == null) return;
        boss.TurnTowards(near.transform.position - boss.transform.position, time, turnSpeed);
    }
}

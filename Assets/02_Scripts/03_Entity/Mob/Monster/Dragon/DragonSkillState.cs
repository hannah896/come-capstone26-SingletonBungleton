using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 지상 패턴 1회 진행 (물기 · 빙탄 3연사 · 브레스). 돌진은 DragonChargeState.
///
/// 판정 시간은 PlayAnim 호출이 아니라 애니메이터가 실제로 클립에 들어간 순간부터 센다(최대 0.8s 대기).
/// - 물기 ★: 선딜(BiteHitTime) 동안 카운터 구간이 열린다. 그 사이 머리를 맞으면 패턴을 끊고 경직으로 간다.
/// - 빙탄 3연사: 서로 다른 플레이어(부족하면 같은 사람)에게 한 발씩. 클립이 루프하므로 SpitInterval마다 발사한다.
/// - 브레스: 뿜는 구간 동안 몸통을 한쪽에서 반대쪽으로 돌리며 틱마다 판정한다.
/// 끝나면 Idle로 돌려놓고 지상 교전 상태로 돌아간다.
/// </summary>
public class DragonSkillState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private readonly DragonSkill skill;
    private readonly List<Player> spitTargets = new List<Player>();

    private bool clipStarted;
    private float waitTimer;
    private float elapsed;
    private bool hitDone;
    private int shotIndex;
    private float tickTimer;
    private float sweepSign;
    private float sweptSoFar;

    public DragonSkillState(DragonBoss owner, DragonStateMachine machine, DragonSkill skill) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
        this.skill = skill;
    }

    public override bool IsAttackState => true;

    private MonsterAnimId Anim => skill switch
    {
        DragonSkill.Bite   => MonsterAnimId.Attack,
        DragonSkill.Spit   => MonsterAnimId.RangeAttack,
        DragonSkill.Breath => MonsterAnimId.Breath,
        _ => MonsterAnimId.Idle,
    };

    public override void OnEnter()
    {
        clipStarted = false;
        waitTimer = MaxClipWait;
        elapsed = 0f;
        hitDone = false;
        shotIndex = 0;
        tickTimer = 0f;
        sweptSoFar = 0f;
        sweepSign = Random.value < 0.5f ? 1f : -1f;

        boss.StartCooldown(skill);
        if (skill == DragonSkill.Bite) boss.SetCounterWindow(true);
        if (skill == DragonSkill.Spit) PickSpitTargets();
        if (skill == DragonSkill.Breath) boss.BeginBreath();
        boss.PlayAnim(Anim);
    }

    public override void OnExit()
    {
        boss.SetCounterWindow(false);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null)
        {
            sm.ToAttack();
            return;
        }

        // 카운터는 클립 진입 대기 중에도 받는다 (파란빛은 애니를 튼 순간부터 보이므로)
        if (skill == DragonSkill.Bite && boss.ConsumeCounter())
        {
            sm.ToCounterStagger();
            return;
        }

        if (!clipStarted)
        {
            Aim(time);
            waitTimer -= time;
            if (boss.IsInClip(Anim) || waitTimer <= 0f)
                clipStarted = true;
            return;
        }

        elapsed += time;

        switch (skill)
        {
            case DragonSkill.Bite:   TickBite(d, time); break;
            case DragonSkill.Spit:   TickSpit(d, time); break;
            case DragonSkill.Breath: TickBreath(d, time); break;
        }
    }

    private void TickBite(DragonStatData d, float time)
    {
        if (!hitDone)
        {
            // 물기 직전까지만 타깃을 따라 돈다 (이후엔 방향 고정 — 옆으로 빠지면 피한다)
            if (elapsed < d.BiteHitTime - 0.25f) Aim(time);

            if (elapsed >= d.BiteHitTime)
            {
                hitDone = true;
                boss.SetCounterWindow(false);
                boss.BiteHit();
            }
        }

        if (elapsed >= d.BiteDuration) End();
    }

    // 서로 다른 플레이어를 가까운 순으로 고르고, 모자라면 앞에서부터 다시 채운다.
    private void PickSpitTargets()
    {
        spitTargets.Clear();
        var players = boss.CollectPlayers();
        int count = Mathf.Max(boss.Data != null ? boss.Data.SpitCount : 3, 1);
        if (players.Count == 0) return;
        for (int i = 0; i < count; i++)
            spitTargets.Add(players[i % players.Count]);
    }

    private void TickSpit(DragonStatData d, float time)
    {
        Player current = shotIndex < spitTargets.Count ? spitTargets[shotIndex] : null;
        if (current != null) boss.TurnTowards(current.transform.position - boss.transform.position, time, d.AimTurnSpeed);

        float releaseAt = d.SpitReleaseTime + d.SpitInterval * shotIndex;
        if (shotIndex < spitTargets.Count && elapsed >= releaseAt)
        {
            if (current != null && current.IsAlive)
                boss.FireFrostBall(current.transform.position, d.SpitFlightTime);
            shotIndex++;
        }

        float total = d.SpitInterval * Mathf.Max(spitTargets.Count, 1);
        if (elapsed >= total) End();
    }

    private void TickBreath(DragonStatData d, float time)
    {
        float window = Mathf.Max(d.BreathEndTime - d.BreathStartTime, 0.1f);
        float half = d.BreathSweepAngle * 0.5f;

        if (elapsed < d.BreathStartTime)
        {
            // 숨 들이쉬는 동안 시작 방향(타깃에서 한쪽으로 반만큼)으로 몸을 틀어 둔다
            if (boss.Target != null)
            {
                Vector3 to = boss.Target.transform.position - boss.transform.position;
                to.y = 0f;
                Vector3 start = Quaternion.Euler(0f, -half * sweepSign, 0f) * to;
                boss.TurnTowards(start, time, d.AimTurnSpeed);
            }
        }
        else if (elapsed <= d.BreathEndTime)
        {
            // 뿜는 동안 반대쪽으로 일정 속도로 쓸어낸다
            float target = d.BreathSweepAngle * Mathf.Clamp01((elapsed - d.BreathStartTime) / window);
            float step = target - sweptSoFar;
            sweptSoFar = target;
            boss.AddYaw(step * sweepSign);

            tickTimer -= time;
            if (tickTimer <= 0f)
            {
                tickTimer = d.BreathTickInterval;
                boss.BreathTick(d.BreathRange);
            }
        }

        if (elapsed >= d.BreathDuration) End();
    }

    private void Aim(float time)
    {
        if (boss.Target == null || boss.Data == null) return;
        boss.TurnTowards(boss.Target.transform.position - boss.transform.position, time, boss.Data.AimTurnSpeed);
    }

    private void End()
    {
        boss.PlayAnim(MonsterAnimId.Idle);
        sm.ToAttack();
    }
}

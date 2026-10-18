using UnityEngine;

/// <summary>
/// 격추 실패: 활공으로 아레나를 한 바퀴 돈 뒤 중앙에 내려앉아 서리 폭발 전멸기를 쓴다.
///
/// ① 선회: Glide 클립으로 아레나 중심 주위 GlideRadius 원을 한 바퀴 돈다
/// ② 진입: 중심을 향해 활공한다
/// ③ 착지: GlideToLanding 클립에 맞춰 루트를 땅까지 내린다
/// ④ 예고: 아레나 전체 경고 원 + 기둥 안전지대 원. ExplosionWarningTime 뒤 폭발
///    (2페이즈 AcidRain: 착지하자마자 월식 폭우 — AcidRainDuration 동안 기둥 밖은 틱 피해)
/// 이 상태 동안은 무적이다 (전멸기 시전을 딜로 끊을 수 없다).
/// </summary>
public class DragonGlideState : MobState<Monster>
{
    private const float MaxClipWait = 0.8f;
    private const float GlideTurnSpeed = 120f;

    private enum Step { Circle, Approach, Land, Warn }

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private Step step;
    private float angle;      // 중심 기준 현재 각도(라디안)
    private float swept;      // 돈 각도 합
    private float elapsed;
    private bool clipStarted;
    private float waitTimer;
    private float rainTick;

    public DragonGlideState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        step = Step.Circle;
        swept = 0f;
        elapsed = 0f;
        Vector3 from = boss.transform.position - boss.ArenaCenter;
        angle = Mathf.Atan2(from.z, from.x);
        boss.SetInvulnerable(true);
        boss.PlayAnim(MonsterAnimId.Glide);
    }

    public override void OnExit()
    {
        boss.SetInvulnerable(false);
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

        float speed = 2f * Mathf.PI * d.GlideRadius / Mathf.Max(d.GlideLapTime, 1f);

        switch (step)
        {
            case Step.Circle:
            {
                float omega = 2f * Mathf.PI / Mathf.Max(d.GlideLapTime, 1f);
                angle += omega * time;
                swept += omega * time;
                Vector3 goal = boss.ArenaCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * d.GlideRadius;
                GlideTowards(goal, speed * 1.3f, time);
                if (swept >= 2f * Mathf.PI) step = Step.Approach;
                break;
            }

            case Step.Approach:
            {
                Vector3 to = boss.ArenaCenter - boss.transform.position;
                to.y = 0f;
                GlideTowards(boss.ArenaCenter, speed, time);
                if (to.magnitude <= 2f)
                {
                    step = Step.Land;
                    clipStarted = false;
                    waitTimer = MaxClipWait;
                    elapsed = 0f;
                    boss.PlayAnim(MonsterAnimId.GlideLand);
                }
                break;
            }

            case Step.Land:
            {
                if (!clipStarted)
                {
                    waitTimer -= time;
                    if (boss.IsInClip(MonsterAnimId.GlideLand) || waitTimer <= 0f) clipStarted = true;
                    break;
                }

                elapsed += time;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(d.LandDescendTime, 0.1f));
                boss.SetAltitude(Mathf.Lerp(d.FlyHeight, 0f, Mathf.SmoothStep(0f, 1f, t)));

                if (elapsed >= d.LandDuration)
                {
                    boss.EndAirPhase();
                    boss.PlayAnim(MonsterAnimId.Idle);
                    if (d.ShootdownFailMode == DragonShootdownFail.AcidRain) boss.BeginAcidRain();
                    else boss.BeginExplosionWarning();
                    step = Step.Warn;
                    elapsed = 0f;
                    rainTick = 0f;
                }
                break;
            }

            case Step.Warn:
                elapsed += time;
                if (d.ShootdownFailMode == DragonShootdownFail.AcidRain)
                {
                    TickAcidRain(d, time);
                    break;
                }
                if (elapsed < d.ExplosionWarningTime) break;
                boss.ExplosionHit();
                sm.ToAttack();
                break;
        }
    }

    // 월식 폭우: 지속 시간 동안 틱마다 기둥 밖 전원에게 피해. 끝나면 교전으로 돌아간다
    private void TickAcidRain(DragonStatData d, float time)
    {
        rainTick -= time;
        if (rainTick <= 0f)
        {
            rainTick = d.AcidRainTickInterval;
            boss.AcidRainTick();
        }
        if (elapsed >= d.AcidRainDuration) sm.ToAttack();
    }

    private void GlideTowards(Vector3 goal, float speed, float time)
    {
        Vector3 to = goal - boss.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        boss.TurnTowards(to, time, GlideTurnSpeed);
        boss.MovePlanar(Vector3.ClampMagnitude(to, speed * time));
    }
}

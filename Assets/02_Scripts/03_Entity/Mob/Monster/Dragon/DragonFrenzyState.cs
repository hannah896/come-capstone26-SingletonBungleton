using UnityEngine;

/// <summary>
/// 최종 발악 (기획 5.6, ×5줄 이하). 한 번만 들어오고, 죽을 때까지 빠져나가지 않는다.
/// ① 남은 기둥이 모두 부서진다
/// ② 아레나 중앙까지 걸어간다
/// ③ 브레스를 뿜은 채 몸을 계속 돌리며 천천히 걷는다 — 회전 반대편(브레스 뒤)을 따라가며 딜해야 한다
/// FrenzyDuration 안에 못 잡으면 광폭화(전원에게 최대 체력 비율 피해). 이후에도 같은 주기로 반복한다.
/// </summary>
public class DragonFrenzyState : MobState<Monster>
{
    private const float CenterArriveDistance = 3f;

    private readonly DragonStateMachine sm;
    private readonly DragonBoss boss;
    private bool breathing;
    private float elapsed;
    private float tickTimer;
    private float turned;     // 한 바퀴 돌 때마다 기둥 부식 목록을 비우기 위한 누적 각도

    public DragonFrenzyState(DragonBoss owner, DragonStateMachine machine) : base(owner, machine)
    {
        boss = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        boss.ConsumeFrenzy();
        boss.SetCounterWindow(false);
        breathing = false;
        elapsed = 0f;
        tickTimer = 0f;
        turned = 0f;

        // 남은 기둥이 모두 부서진다
        foreach (var cover in DragonCover.All)
        {
            if (cover == null) continue;
            while (!cover.IsBroken) cover.Corrode();
        }

        boss.PlayAnim(MonsterAnimId.Move);
    }

    public override void Update(float time = 1.0f)
    {
        var d = boss.Data;
        if (d == null) return;

        if (!breathing)
        {
            Vector3 to = boss.ArenaCenter - boss.transform.position;
            to.y = 0f;
            if (to.magnitude > CenterArriveDistance)
            {
                boss.TurnTowards(to, time, 120f);
                boss.MovePlanar(boss.transform.forward * (d.FrenzyWalkSpeed * 2f * time));
                return;
            }

            breathing = true;
            boss.BeginBreath();
            boss.PlayAnim(MonsterAnimId.Frenzy);
        }

        // 몸을 돌리며 천천히 걷는다 (작은 원을 그린다)
        float yaw = d.FrenzyTurnSpeed * time;
        boss.AddYaw(yaw);
        boss.MovePlanar(boss.transform.forward * (d.FrenzyWalkSpeed * time));

        turned += yaw;
        if (turned >= 360f)
        {
            turned -= 360f;
            boss.BeginBreath();
        }

        tickTimer -= time;
        if (tickTimer <= 0f)
        {
            tickTimer = d.BreathTickInterval;
            boss.BreathTick(d.FrenzyRange);
        }

        elapsed += time;
        if (elapsed >= d.FrenzyDuration)
        {
            elapsed = 0f;
            boss.EnrageHit();
        }
    }
}

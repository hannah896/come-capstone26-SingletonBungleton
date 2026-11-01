using SingletonBungleton.BossTraining;
using UnityEngine;

/// <summary>
/// 본편 드래곤의 "지금 상황"을 학습 판단층(IDragonBrain)이 읽는 형태로 바꿔 주는 어댑터.
/// 학습 아레나(DragonTrainingArena)가 채우는 것과 같은 <see cref="DragonSituation"/>을 만들기 때문에,
/// 시뮬레이션에서 학습한 모델이 본편에서도 같은 뜻의 입력을 받는다.
///
/// DragonBoss의 쿨타임 배열은 비공개라서, 패턴이 쿨타임에 들어가는 순간(IsSkillReady가 true → false)을 지켜보며 남은 비율을 추정한다.
/// </summary>
[RequireComponent(typeof(DragonBoss))]
public class DragonSituationProvider : MonoBehaviour, IDragonSituationSource
{
    private const int SkillCount = 4;

    private DragonBoss boss;
    private bool subscribed;

    private readonly bool[] wasReady = new bool[SkillCount];
    private readonly float[] cooldownStart = new float[SkillCount];

    private Player trackedTarget;
    private Vector3 targetVelocity;

    private void Awake()
    {
        boss = GetComponent<DragonBoss>();
    }

    private void OnEnable()
    {
        for (int i = 0; i < SkillCount; i++) wasReady[i] = true;
        trackedTarget = null;

        if (subscribed || Main.Instance == null) return;
        Main.Loop.OnUpdate += OnLoopUpdate;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        if (Main.Instance != null && Main.Loop != null) Main.Loop.OnUpdate -= OnLoopUpdate;
        subscribed = false;
    }

    // 쿨타임 시작 시각 추적 + 타깃 속도 추정 (판단은 0.5초 간격이라 매 프레임 계산은 가볍게 한다)
    private void OnLoopUpdate(float deltaTime)
    {
        for (int i = 0; i < SkillCount; i++)
        {
            bool ready = boss.IsSkillReady((DragonSkill)i);
            if (wasReady[i] && !ready) cooldownStart[i] = Time.time;
            wasReady[i] = ready;
        }

        Player target = boss.Target;
        if (target != trackedTarget)
        {
            trackedTarget = target;
            targetVelocity = Vector3.zero;
        }
        else if (target != null && target.Motor != null)
        {
            targetVelocity = target.Motor.Velocity;
        }
    }

    public bool TryGetSituation(out DragonSituation s)
    {
        s = default;
        var data = boss != null ? boss.Data : null;
        Player target = boss != null ? boss.Target : null;
        if (data == null || target == null) return false;

        Vector3 me = boss.transform.position;
        Vector3 to = target.transform.position - me;
        to.y = 0f;
        float dist = to.magnitude;
        Vector3 away = dist > 0.001f ? to / dist : boss.transform.forward;

        Vector3 flatForward = boss.transform.forward;
        flatForward.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, flatForward.normalized);

        s.distance = dist;
        s.angleToTarget = dist > 0.001f ? Vector3.SignedAngle(flatForward, to, Vector3.up) : 0f;
        s.targetRadialSpeed = Vector3.Dot(targetVelocity, away);
        s.targetTangentSpeed = Vector3.Dot(targetVelocity, right);

        s.hpRatio = boss.HpRatio;
        var stat = target.Stat;
        s.targetHpRatio = stat != null && stat.MaxHp > 0f ? Mathf.Clamp01(stat.CurrentHp / stat.MaxHp) : 1f;

        s.biteCooldown = CooldownRatio(DragonSkill.Bite, data.BiteCooldown);
        s.breathCooldown = CooldownRatio(DragonSkill.Breath, data.BreathCooldown);
        s.chargeCooldown = CooldownRatio(DragonSkill.Charge, data.ChargeCooldown);
        s.spitCooldown = CooldownRatio(DragonSkill.Spit, data.SpitCooldown);

        float radius = Mathf.Max(1f, data.ArenaRadius);
        Vector3 center = boss.ArenaCenter;
        s.selfPos = new Vector2(me.x - center.x, me.z - center.z) / radius;
        Vector3 tp = target.transform.position;
        s.targetPos = new Vector2(tp.x - center.x, tp.z - center.z) / radius;

        s.biteRange = data.BiteRange;
        s.breathRange = data.BreathRange;
        s.chargeMinRange = data.ChargeMinRange;
        s.spitMinRange = data.SpitMinRange;
        s.arenaSize = radius * 2f;
        s.speedNorm = 11f;   // 학습 아레나의 Max(10, 플레이어 이동 속도 x 2.2)와 같은 기준
        return true;
    }

    // 쿨타임이 끝났으면 0, 진행 중이면 (남은 시간 / 전체 시간). 시작 시각을 못 본 경우(켜지기 전부터 쿨타임)는 1로 둔다.
    private float CooldownRatio(DragonSkill skill, float total)
    {
        if (boss.IsSkillReady(skill) || total <= 0f) return 0f;

        int i = (int)skill;
        float elapsed = cooldownStart[i] > 0f ? Time.time - cooldownStart[i] : 0f;
        return Mathf.Clamp01(1f - elapsed / total);
    }
}

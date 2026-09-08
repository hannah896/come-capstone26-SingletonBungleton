/// <summary>
/// 데빌 비행 공격 상태. 공중에 떠 있는 동안의 스킬 선택을 담당한다.
///
/// 우선순위 — 위협적인 기술일수록 먼저 검사한다.
/// ① 지옥 세례 (쿨 준비 + AttackRange 이내) : 타깃 발밑에 장판. 강제 이동을 만든다
/// ② 꼬리치기  (쿨 준비 + TailRange 이내)   : 붙은 플레이어를 떼어낸다
/// ③ 급강하    (쿨 준비 + DiveRange 이내)   : 내려찍고 일시 착지
/// ④ 화염 연사 (쿨 준비)                    : 그 외 거리에서의 기본 견제
/// ⑤ 접근                                   : 전부 쿨이면 비행 이동으로 거리를 좁힌다
///
/// 비행 지속 시간이 끝나면 스스로 착지한다.
/// </summary>
public class DevilFlyAttackState : MobState<Monster>
{
    private readonly DevilStateMachine sm;
    private readonly Devil devil;

    private float castTimer;    // 시전 모션 남은 시간 (>0이면 제자리 고정)

    public DevilFlyAttackState(Devil owner, DevilStateMachine machine) : base(owner, machine)
    {
        devil = owner;
        sm = machine;
    }

    public override bool IsAttackState => true;

    public override void OnEnter()
    {
        castTimer = 0f;
        devil.PlayAnim(MonsterAnimId.FlyIdle);
    }

    public override void Update(float time = 1.0f)
    {
        // 시전 중에는 제자리 고정. 착지 판정도 미뤄 모션이 끊기지 않게 한다.
        if (castTimer > 0f)
        {
            castTimer -= time;
            return;
        }

        // 비행 시간 종료 → 착지
        if (devil.IsFlyTimeOver)
        {
            sm.ToLand();
            return;
        }

        if (!devil.IsTargetValid())
        {
            devil.ClearTarget();
            sm.ToLand();
            return;
        }

        devil.FaceTargetStep(time);

        // ① 지옥 세례
        if (devil.IsHellRainReady && devil.IsTargetInAttackRange())
        {
            devil.CastHellRain();
            castTimer = devil.HellRainCastTime;
            return;
        }

        // ② 꼬리치기 — 붙었을 때 떼어낸다
        if (devil.IsTailReady && devil.IsTargetInTailRange())
        {
            devil.PerformTailAttack();
            castTimer = devil.TailCastTime;
            return;
        }

        // ③ 급강하
        if (devil.IsDiveReady && devil.IsTargetInDiveRange())
        {
            sm.ToDive();
            return;
        }

        // ④ 화염 연사
        if (devil.IsFlyProjectileReady && devil.IsTargetInAttackRange())
        {
            devil.FireFlyProjectileBurst();
            castTimer = devil.FlyProjectileCastTime;
            return;
        }

        // ⑤ 전부 쿨 대기: 비행 이동으로 접근
        devil.PlayAnim(MonsterAnimId.FlyMove);
        devil.FlyChaseStep(time);
    }
}

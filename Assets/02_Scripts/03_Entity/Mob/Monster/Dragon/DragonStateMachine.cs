/// <summary>
/// 드래곤 보스 전용 상태 머신.
/// - 공격 상태를 DragonGroundState(지상 패턴 선택 + 접근)로 교체한다. 추적도 공격 상태가 직접 한다.
/// - 피격 경직(Hit)은 없다(슈퍼아머). 무력화가 깨지면 그로기, 카운터를 맞으면 짧은 경직.
/// - 지상 패턴(DragonSkillState·DragonChargeState), 공중 페이즈(이륙 → 체공 → 추락/활공)를 더한다.
/// Spawn/Idle/Dead는 베이스 Monster 상태를 그대로 사용한다.
/// </summary>
public class DragonStateMachine : MonsterStateMachine
{
    private readonly DragonBoss boss;

    public DragonStateMachine(DragonBoss owner) : base(owner)
    {
        boss = owner;
    }

    // 학습된 판단층(IDragonBrain)이 준비돼 있으면 그쪽이 패턴을 고르고, 없으면 기존 규칙 기반 선택을 쓴다.
    public override void ToAttack()
    {
        if (DragonAiGroundState.CanUse(boss, out var brain))
            ChangeState(new DragonAiGroundState(boss, this, brain));
        else
            ChangeState(new DragonGroundState(boss, this));
    }

    public override void ToChase()
        => ToAttack();

    // 슈퍼아머: 맞아도 움찔하지 않는다.
    public override void ToHit() { }

    /// <summary>지상 패턴 1회. 돌진은 전용 상태로 간다.</summary>
    public void ToSkill(DragonSkill skill)
    {
        if (skill == DragonSkill.Charge) ChangeState(new DragonChargeState(boss, this));
        else ChangeState(new DragonSkillState(boss, this, skill));
    }

    /// <summary>그로기 (무력화 파괴 · 기둥 충돌 · 격추). holdAnim은 그로기 동안 틀 애니.</summary>
    public void ToGroggy(float duration, float damageMultiplier, MonsterAnimId holdAnim = MonsterAnimId.Hit)
        => ChangeState(new DragonGroggyState(boss, this, duration, damageMultiplier, holdAnim));

    /// <summary>카운터 경직 (GetHit2). 끝날 때 무력화가 바닥났으면 그로기로 넘어간다.</summary>
    public void ToCounterStagger()
        => ChangeState(new DragonStaggerState(boss, this));

    public void ToTakeOff()
        => ChangeState(new DragonTakeOffState(boss, this));

    public void ToAir()
        => ChangeState(new DragonAirState(boss, this));

    /// <summary>격추 성공: 추락 → 충돌 → 그로기.</summary>
    public void ToFall()
        => ChangeState(new DragonFallState(boss, this));

    /// <summary>격추 실패: 활공 선회 → 착지 → 서리 폭발(1페이즈) 또는 월식 폭우(2페이즈).</summary>
    public void ToGlide()
        => ChangeState(new DragonGlideState(boss, this));

    /// <summary>페이즈 전환 등장 연출: 이륙 → 포효 → 착지 충격파.</summary>
    public void ToIntro()
        => ChangeState(new DragonIntroState(boss, this));

    /// <summary>월식의 응시: 중앙 상공에서 카운트다운 → 카메라 판정 → 착지 후 산성 침.</summary>
    public void ToGaze()
        => ChangeState(new DragonGazeState(boss, this));

    /// <summary>최종 발악: 중앙에서 브레스를 360° 돌리며 걷는다.</summary>
    public void ToFrenzy()
        => ChangeState(new DragonFrenzyState(boss, this));
}

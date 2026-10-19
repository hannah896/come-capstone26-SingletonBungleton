/// <summary>
/// Devil 전용 상태 머신. 자세(Ground/Fly)에 따라 공격 상태를 갈라 보내고,
/// 이륙/착지/급강하 전환을 추가한다. Spawn/Idle/Hit/Dead는 베이스 Monster 상태를 그대로 쓴다.
/// </summary>
public class DevilStateMachine : MonsterStateMachine
{
    private readonly Devil devil;

    public DevilStateMachine(Devil owner) : base(owner)
    {
        devil = owner;
    }

    /// <summary>현재 자세에 맞는 공격 상태로 보낸다.</summary>
    public override void ToAttack()
    {
        if (devil.IsFlying)
            ChangeState(new DevilFlyAttackState(devil, this));
        else
            ChangeState(new DevilGroundAttackState(devil, this));
    }

    /// <summary>이륙 전환 (지상 → 비행).</summary>
    public void ToTakeOff()
        => ChangeState(new DevilTakeOffState(devil, this));

    /// <summary>착지 전환 (비행 → 지상).</summary>
    public void ToLand()
        => ChangeState(new DevilLandState(devil, this));

    /// <summary>급강하 할퀴기. 착지 지점 광역 피해 후 지상 자세로 내려앉는다.</summary>
    public void ToDive()
        => ChangeState(new DevilDiveState(devil, this));

    /// <summary>진짜 찾기 시전: 연기 속으로 사라졌다가 분신과 함께 나타난다.</summary>
    public void ToCloneCast()
        => ChangeState(new DevilCloneCastState(devil, this));

    /// <summary>진짜를 들켰다: 분신이 사라지고 잠시 비틀거린다.</summary>
    public void ToCloneReveal()
        => ChangeState(new DevilCloneRevealState(devil, this));
}

/// <summary>
/// 몬스터 애니메이션 식별자. 호스트가 재생한 애니를 클라가 그대로 재현하기 위해 네트워크로 복제된다.
/// 실제 애니메이터 Bool 파라미터명은 몬스터 종류마다 다르므로, 이 ID를 각 Monster가 자기 해시로 매핑한다.
/// (Monster.AnimBoolHash / Mischief.AnimBoolHash 참고)
///
/// byte로 복제되므로 값을 바꾸거나 중간을 지우지 말 것. 추가는 뒤에만 한다.
/// </summary>
public enum MonsterAnimId : byte
{
    None = 0,
    Idle = 1,
    Move = 2,
    Attack = 3,
    /// <summary>Mischief 등 원거리 공격 보유 몬스터 전용.</summary>
    RangeAttack = 4,
    Hit = 5,
    Dead = 6,
    /// <summary>Mischief 도약 등 이동 연출 전용.</summary>
    Jump = 7,

    // ── 이하 Devil(임프 데빌) 전용. 비행/잠복 2모드를 갖는 몬스터에서만 사용한다. ──
    /// <summary>비행 대기 (Fly Idle).</summary>
    FlyIdle = 8,
    /// <summary>비행 이동 (Fly Forward In Place).</summary>
    FlyMove = 9,
    /// <summary>비행 근접 — 급강하 할퀴기 (Fly Slash Attack).</summary>
    FlyAttack = 10,
    /// <summary>비행 원거리 — 화염 연사 (Fly Projectile Attack).</summary>
    FlyRangeAttack = 11,
    /// <summary>비행 근접 — 꼬리치기 (Fly Tail Attack).</summary>
    FlyTail = 12,
    /// <summary>비행 광역 — 지옥 세례 (Fly Cast Spell).</summary>
    FlyCast = 13,
    /// <summary>이륙 전환 (Idle To Fly Idle).</summary>
    TakeOff = 14,
    /// <summary>잠복 (Underground). ※ P3 예정 — 현재 미사용.</summary>
    Underground = 15,

    // ── 이하 TreantMinion(트리앤트 미니언) 전용. 구르기로 이동하는 몬스터에서만 사용한다. ──
    /// <summary>구르기 돌진 (Roll Attack).</summary>
    RollAttack = 16,
    /// <summary>주문 시전 — 뿌리 속박 (Cast Spell).</summary>
    CastSpell = 17,

    // ── 이하 Treeguard(만월 보스 트리가드) 전용. 근접 5종 + 걷기/뛰기 2단 이동. ──
    /// <summary>뛰기 (Run Forward In Place). 3페이즈 이동. 평소 이동(Walk)은 Move를 쓴다.</summary>
    Run = 18,
    /// <summary>고목 내려찍기 (Smack Attack).</summary>
    Smack = 19,
    /// <summary>밀쳐내기 (Kick Attack).</summary>
    Kick = 20,
    /// <summary>대지 울림 (Step Attack).</summary>
    Step = 21,
    /// <summary>박수 충격파 (Clap Attack).</summary>
    Clap = 22,
}

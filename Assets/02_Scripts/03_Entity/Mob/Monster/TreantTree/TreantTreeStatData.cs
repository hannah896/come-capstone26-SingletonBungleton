using UnityEngine;

/// <summary>
/// 트리앤트 트리 전용 스탯 SO.
/// 베이스 AttackRange가 "교전 사거리"(공격 상태 진입 거리)이고, 그 안에서 두 기술을 거리와 쿨타임으로 골라 쓴다.
/// - 나무 주먹(Punch Attack): 주력기. 전방 부채꼴의 플레이어를 크게 밀어낸다
/// - 열매 투척(Projectile Attack): 주먹이 닿지 않는 거리에서 쓰는 견제기
/// </summary>
[CreateAssetMenu(fileName = "TreantTreeStatData", menuName = "Scriptable Objects/TreantTreeStatData")]
public class TreantTreeStatData : MonsterStatData
{
    [Header("트리앤트 트리 - 나무 주먹 (Punch Attack)")]
    [Tooltip("주먹을 휘두르기 시작하는 거리. AttackRange보다 짧게 둔다")]
    public float PunchRange = 2.2f;
    [Tooltip("주먹 재사용 대기시간(초)")]
    public float PunchCooldown = 2.5f;
    [Tooltip("모션 시작부터 실제 타격까지의 시간(초). 이 동안 플레이어가 빠져나갈 수 있다")]
    public float PunchHitTime = 0.4f;
    [Tooltip("주먹 모션 전체 동안 제자리에 고정되는 시간(초)")]
    public float PunchDuration = 0.9f;
    [Tooltip("타격 판정 부채꼴 각도(도). 몸 정면 기준")]
    [Range(0f, 360f)] public float PunchAngle = 120f;
    [Tooltip("주먹 데미지. 0 이하면 AttackDamage 사용")]
    public float PunchDamage = 0f;
    [Tooltip("주먹 명중 시 밀어내는 힘. 미니언 구르기(6)보다 확실히 크게 둔다")]
    public float PunchKnockback = 14f;
    [Tooltip("넉백이 유지되는 시간(초)")]
    public float PunchKnockbackDuration = 0.4f;

    [Header("트리앤트 트리 - 열매 투척 (Projectile Attack)")]
    [Tooltip("투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "TreantProjectile";
    [Tooltip("투척 재사용 대기시간(초)")]
    public float ProjectileCooldown = 3.5f;
    [Tooltip("투척 모션 동안 제자리에 고정되는 시간(초)")]
    public float ProjectileCastTime = 0.8f;
    [Tooltip("투사체 이동 속도")]
    public float ProjectileSpeed = 11f;
    [Tooltip("투사체 생존 시간(초). 초과 시 미명중으로 회수")]
    public float ProjectileLifeTime = 3f;
    [Tooltip("투사체 데미지. 0 이하면 AttackDamage 사용")]
    public float ProjectileDamage = 0f;
    [Tooltip("발사 높이(지면 기준). 발사 지점과 조준점 모두에 적용된다")]
    public float MuzzleHeight = 1.2f;
}

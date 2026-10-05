using UnityEngine;

/// <summary>
/// 트리앤트 미니언 전용 스탯 SO.
/// 베이스 AttackRange가 "교전 사거리"(공격 상태 진입 거리)이고, 그 안에서 세 기술을 쿨타임으로 돌려 쓴다.
/// - 뿌리 속박(Cast Spell): 가장 긴 쿨. 발을 묶어 다음 돌진을 맞히기 위한 선행기
/// - 구르기 돌진(Roll Attack): 주력 딜기. 타깃을 통과하며 들이받는다
/// - 도토리 투척(Projectile Attack): 둘 다 쿨일 때 쓰는 견제기
/// </summary>
[CreateAssetMenu(fileName = "TreantMinionStatData", menuName = "Scriptable Objects/TreantMinionStatData")]
public class TreantMinionStatData : MonsterStatData
{
    [Header("트리앤트 - 구르기 돌진 (Roll Attack)")]
    [Tooltip("돌진을 시작할 수 있는 최대 거리. AttackRange보다 짧게 둔다")]
    public float RollRange = 8f;
    [Tooltip("돌진 재사용 대기시간(초)")]
    public float RollCooldown = 4f;
    [Tooltip("돌진 중 전진 속도(m/s)")]
    public float RollSpeed = 9f;
    [Tooltip("돌진 지속 시간(초). RollSpeed와 곱한 만큼 직선으로 나아간다")]
    public float RollDuration = 0.55f;
    [Tooltip("돌진 중 몸통 판정 반경. 이 안에 들어온 플레이어를 들이받는다")]
    public float RollHitRadius = 0.8f;
    [Tooltip("돌진 명중 데미지. 0 이하면 AttackDamage 사용")]
    public float RollDamage = 0f;
    [Tooltip("돌진 명중 시 밀어내는 힘. 0이면 넉백 없음")]
    public float RollKnockback = 6f;
    [Tooltip("넉백이 유지되는 시간(초)")]
    public float RollKnockbackDuration = 0.25f;
    [Tooltip("돌진이 끝난 뒤 멈춰서 숨을 고르는 시간(초). 이 동안은 무방비다")]
    public float RollRecoverTime = 0.4f;

    [Header("트리앤트 - 도토리 투척 (Projectile Attack)")]
    [Tooltip("투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "TreantProjectile";
    [Tooltip("투척 재사용 대기시간(초)")]
    public float ProjectileCooldown = 2.5f;
    [Tooltip("투척 모션 동안 제자리에 고정되는 시간(초)")]
    public float ProjectileCastTime = 0.6f;
    [Tooltip("투사체 이동 속도")]
    public float ProjectileSpeed = 12f;
    [Tooltip("투사체 생존 시간(초). 초과 시 미명중으로 회수")]
    public float ProjectileLifeTime = 3f;
    [Tooltip("투사체 데미지. 0 이하면 AttackDamage 사용")]
    public float ProjectileDamage = 0f;
    [Tooltip("발사 높이(지면 기준). 발사 지점과 조준점 모두에 적용된다")]
    public float MuzzleHeight = 0.8f;

    [Header("트리앤트 - 뿌리 속박 (Cast Spell)")]
    [Tooltip("장판 프리팹의 Addressable 키. 범용 장판(Meteor) 컴포넌트를 쓴다")]
    public string SnareKey = "RootSnare";
    [Tooltip("뿌리 속박을 시전할 수 있는 최대 거리")]
    public float SnareRange = 8f;
    [Tooltip("뿌리 속박 재사용 대기시간(초)")]
    public float SnareCooldown = 8f;
    [Tooltip("시전 모션 동안 제자리에 고정되는 시간(초)")]
    public float SnareCastTime = 1f;
    [Tooltip("장판이 솟아오르기까지의 예고 시간(초). 플레이어가 피할 수 있는 창")]
    public float SnareWarningTime = 0.7f;
    [Tooltip("발동 후 이펙트가 잦아들 때까지 기다리는 시간(초)")]
    public float SnareLingerTime = 1.5f;
    [Tooltip("장판 판정 반경")]
    public float SnareRadius = 2f;
    [Tooltip("장판 명중 데미지. 속박이 본체라 데미지는 낮게 둔다")]
    public float SnareDamage = 3f;
    [Tooltip("명중 시 적용할 이동 속도 배율(0~1). 0.45면 속도가 45%로 떨어진다")]
    [Range(0f, 1f)] public float SnareSlowMultiplier = 0.45f;
    [Tooltip("둔화 지속 시간(초)")]
    public float SnareSlowDuration = 3f;
}

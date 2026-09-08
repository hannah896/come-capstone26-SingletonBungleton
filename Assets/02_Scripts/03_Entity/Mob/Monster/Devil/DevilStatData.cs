using UnityEngine;

/// <summary>
/// 임프 데빌 전용 스탯 SO. (기획: docs/기획_데빌_스킬로직.md)
///
/// 데빌은 지상/비행 2자세를 가진다.
/// - 지상: 할퀴기(근접) + 화염구(중거리). 의도적으로 평범하게 둔다.
/// - 비행: 급강하·꼬리치기·화염 연사·지옥 세례. 근접이 닿지 않는 대신 강한 기술을 쓴다.
///
/// 베이스 AttackRange가 "공격 상태 진입 거리" 역할을 하고, 그보다 짧은 SlashRange에서 근접으로 전환한다.
/// </summary>
[CreateAssetMenu(fileName = "DevilStatData", menuName = "Scriptable Objects/DevilStatData")]
public class DevilStatData : MonsterStatData
{
    [Header("데빌 - 지상: 할퀴기")]
    [Tooltip("근접 할퀴기 사거리. AttackRange보다 짧게 설정한다")]
    public float SlashRange = 2f;

    [Header("데빌 - 지상: 화염구")]
    [Tooltip("화염구 투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "MischiefProjectile";
    [Tooltip("화염구 내부 쿨타임(초)")]
    public float FireballCooldown = 3f;
    [Tooltip("발사 모션 동안 제자리에 고정되는 시간(초)")]
    public float FireballCastTime = 0.8f;
    [Tooltip("투사체 속도(m/s)")]
    public float ProjectileSpeed = 10f;
    [Tooltip("투사체 최대 생존 시간(초)")]
    public float ProjectileLifeTime = 3f;
    [Tooltip("투사체 1발 데미지. 0 이하면 AttackDamage 사용")]
    public float ProjectileDamage = 0f;
    [Tooltip("발사 지점 높이(발밑 기준 m)")]
    public float MuzzleHeight = 1f;

    [Header("데빌 - 자세 전환")]
    [Tooltip("이 횟수만큼 피격당하면 이륙한다")]
    public int HitsToTakeOff = 3;
    [Tooltip("플레이어가 이 거리 안에 NearContactTime 이상 머물면 이륙한다")]
    public float NearContactRange = 2f;
    [Tooltip("밀착 판정 지속 시간(초)")]
    public float NearContactTime = 2f;
    [Tooltip("이 HP 비율에 처음 도달하면 강제로 1회 이륙한다 (페이즈 2 진입)")]
    public float Phase2HpRatio = 0.7f;
    [Tooltip("착지 후 다시 이륙하기까지의 대기시간(초). 플레이어의 딜 타임을 보장한다")]
    public float TakeOffCooldown = 8f;
    [Tooltip("한 번 이륙했을 때 공중에 머무는 시간(초). 지나면 스스로 착지한다")]
    public float FlyDuration = 12f;

    [Header("데빌 - 비행: 고도")]
    [Tooltip("비행 시 루트가 떠오르는 높이(m). 콜라이더도 함께 올라가 근접 공격이 물리적으로 닿지 않는다")]
    public float FlyHeight = 3.5f;
    [Tooltip("이륙 연출 시간(초)")]
    public float TakeOffDuration = 0.8f;
    [Tooltip("착지 연출 시간(초)")]
    public float LandDuration = 0.5f;
    [Tooltip("비행 중 이동 속도 배율 (지상 MoveSpeed 대비)")]
    public float FlyMoveSpeedMultiplier = 1.2f;

    [Header("데빌 - 비행: 급강하 할퀴기")]
    [Tooltip("급강하 내부 쿨타임(초)")]
    public float DiveCooldown = 5f;
    [Tooltip("이 거리 안일 때 급강하를 시도한다(m)")]
    public float DiveRange = 6f;
    [Tooltip("급강하에 걸리는 시간(초)")]
    public float DiveDuration = 0.5f;
    [Tooltip("착지 지점 광역 피해 반경(m)")]
    public float DiveImpactRadius = 2f;
    [Tooltip("급강하 데미지. 0 이하면 AttackDamage 사용")]
    public float DiveDamage = 0f;

    [Header("데빌 - 비행: 꼬리치기")]
    [Tooltip("꼬리치기 내부 쿨타임(초)")]
    public float TailCooldown = 4f;
    [Tooltip("꼬리치기 사거리(m). 붙은 플레이어를 떼어내는 용도라 짧다")]
    public float TailRange = 3f;
    [Tooltip("꼬리치기 모션 동안 제자리에 고정되는 시간(초)")]
    public float TailCastTime = 0.6f;
    [Tooltip("넉백 세기(m/s)")]
    public float TailKnockback = 8f;
    [Tooltip("넉백이 감쇠하며 유지되는 시간(초)")]
    public float TailKnockbackDuration = 0.3f;
    [Tooltip("꼬리치기 데미지. 0 이하면 AttackDamage 사용")]
    public float TailDamage = 0f;

    [Header("데빌 - 비행: 화염 연사")]
    [Tooltip("화염 연사 내부 쿨타임(초)")]
    public float FlyProjectileCooldown = 2f;
    [Tooltip("연사 발수")]
    public int FlyProjectileBurst = 3;
    [Tooltip("연사 간격(초)")]
    public float FlyProjectileInterval = 0.15f;
    [Tooltip("연사 모션 동안 제자리에 고정되는 시간(초)")]
    public float FlyProjectileCastTime = 0.8f;

    [Header("데빌 - 비행: 지옥 세례 (Meteor 재사용)")]
    [Tooltip("운석 프리팹의 Addressable 키")]
    public string MeteorKey = "Meteor";
    [Tooltip("지옥 세례 내부 쿨타임(초)")]
    public float HellRainCooldown = 10f;
    [Tooltip("시전 모션 동안 제자리에 고정되는 시간(초)")]
    public float HellRainCastTime = 1.2f;
    [Tooltip("한 번의 세례로 떨어뜨릴 운석 개수")]
    public int MeteorCount = 6;
    [Tooltip("타깃을 중심으로 운석이 떨어지는 원의 반지름(m)")]
    public float MeteorRadius = 5f;
    [Tooltip("폭발 전 예고(바닥 장판) 시간(초)")]
    public float MeteorWarningTime = 0.8f;
    [Tooltip("장판마다 예고 시간을 이만큼씩 늘려 순차적으로 터지게 한다(초)")]
    public float MeteorSpawnStagger = 0.12f;
    [Tooltip("폭발 시 광역 데미지 반경(m)")]
    public float MeteorImpactRadius = 1.5f;
    [Tooltip("폭발 후 이펙트가 다 재생될 때까지 기다리는 시간(초)")]
    public float MeteorLingerTime = 1.5f;
    [Tooltip("운석 1발의 데미지. 0 이하면 AttackDamage 사용")]
    public float MeteorDamage = 0f;
}

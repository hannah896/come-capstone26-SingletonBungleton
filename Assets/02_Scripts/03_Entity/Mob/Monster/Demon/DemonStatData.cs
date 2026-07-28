using UnityEngine;

/// <summary>
/// Demon 전용 스탯 SO.
/// Mischief와 같은 근접+원거리 하이브리드 구조지만, 원거리 공격이 단일 투사체가 아니라
/// "자기 주변 반경 MeteorRadius 원 안 MeteorCount개 지점에 운석을 떨구는" 광역 운석 세례(Meteor Storm)다.
/// - 슬래시: 내부 쿨 없음 (MinAttackPeriod 주기만 적용)
/// - 운석 세례: 넓은 반경을 덮는 대신 긴 내부 쿨타임(MeteorCooldown) 적용
/// 베이스 AttackRange가 "운석 세례 진입 거리"(공격 상태 진입 거리) 역할을 하고,
/// 그보다 짧은 SlashRange 이내에서는 근접 슬래시로 전환한다.
/// </summary>
[CreateAssetMenu(fileName = "DemonStatData", menuName = "Scriptable Objects/DemonStatData")]
public class DemonStatData : MonsterStatData
{
    [Header("데몬 - 슬래시")]
    [Tooltip("근접 슬래시 사거리. AttackRange(운석 세례 진입 거리)보다 짧게 설정한다")]
    public float SlashRange = 2f;

    [Header("데몬 - 운석 세례(Meteor Storm)")]
    [Tooltip("운석 프리팹의 Addressable 키")]
    public string MeteorKey = "Meteor";
    [Tooltip("운석 세례 내부 쿨타임(초). 슬래시와 별개로 돈다")]
    public float MeteorCooldown = 7f;
    [Tooltip("한 번의 세례로 떨어뜨릴 운석 개수(k)")]
    public int MeteorCount = 6;
    [Tooltip("데몬을 중심으로 운석이 떨어지는 원의 반지름(r, m)")]
    public float MeteorRadius = 5f;
    [Tooltip("운석 낙하 시작 높이(지면 기준, m)")]
    public float MeteorSpawnHeight = 12f;
    [Tooltip("운석 낙하 속도(m/s)")]
    public float MeteorFallSpeed = 18f;
    [Tooltip("착탄 전 예고(경고 표식) 시간(초). 이 시간 뒤 낙하를 시작한다. 0이면 즉시 낙하")]
    public float MeteorWarningTime = 0.8f;
    [Tooltip("운석마다 예고 시간을 이만큼씩 늘려 순차적으로 떨어지게 한다(초). 0이면 전부 동시")]
    public float MeteorSpawnStagger = 0.12f;
    [Tooltip("착탄 시 광역 데미지 반경(m)")]
    public float MeteorImpactRadius = 1.5f;
    [Tooltip("운석 1발의 데미지. 0 이하면 AttackDamage 사용")]
    public float MeteorDamage = 0f;
}

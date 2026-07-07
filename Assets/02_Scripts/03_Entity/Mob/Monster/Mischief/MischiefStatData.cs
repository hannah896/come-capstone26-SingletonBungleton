using UnityEngine;

/// <summary>
/// Mischief 전용 스탯 SO.
/// 베이스 AttackRange가 "프로젝타일 사거리"(공격 상태 진입 거리) 역할을 하고,
/// 그보다 짧은 SlashRange 이내에서는 근접 슬래시로 전환한다.
/// - 슬래시: 내부 쿨 없음 (MinAttackPeriod 주기만 적용)
/// - 프로젝타일: 사거리가 넓은 대신 긴 내부 쿨타임(ProjectileCooldown) 적용
/// </summary>
[CreateAssetMenu(fileName = "MischiefStatData", menuName = "Scriptable Objects/MischiefStatData")]
public class MischiefStatData : MonsterStatData
{
    [Header("미스치프 - 슬래시")]
    [Tooltip("근접 슬래시 사거리. AttackRange(프로젝타일 사거리)보다 짧게 설정한다")]
    public float SlashRange = 1.5f;

    [Header("미스치프 - 프로젝타일")]
    [Tooltip("투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "MischiefProjectile";
    [Tooltip("프로젝타일 내부 쿨타임(초). 슬래시와 별개로 돈다")]
    public float ProjectileCooldown = 5f;
    [Tooltip("투사체 이동 속도")]
    public float ProjectileSpeed = 10f;
    [Tooltip("투사체 생존 시간(초). 초과 시 미명중으로 회수")]
    public float ProjectileLifeTime = 3f;
    [Tooltip("투사체 데미지. 0 이하면 AttackDamage 사용")]
    public float ProjectileDamage = 0f;
    [Tooltip("발사 높이(지면 기준). 발사 지점과 조준점 모두에 적용된다")]
    public float MuzzleHeight = 1f;
}

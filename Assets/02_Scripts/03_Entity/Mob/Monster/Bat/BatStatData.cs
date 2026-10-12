using UnityEngine;

/// <summary>
/// 외눈 박쥐(Cyclops Bat) 전용 스탯 SO.
/// 베이스 AttackRange가 "눈알 탄 사거리"(공격 상태 진입 거리) 역할을 하고,
/// 그보다 짧은 BiteRange 이내에서는 근접 물기로 전환한다.
/// - 물기: 내부 쿨 없음 (MinAttackPeriod 주기만 적용)
/// - 눈알 탄: 사거리가 넓은 대신 내부 쿨타임(ProjectileCooldown) 적용
/// </summary>
[CreateAssetMenu(fileName = "BatStatData", menuName = "Scriptable Objects/BatStatData")]
public class BatStatData : MonsterStatData
{
    [Header("외눈 박쥐 - 물기")]
    [Tooltip("근접 물기 사거리. AttackRange(눈알 탄 사거리)보다 짧게 설정한다")]
    public float BiteRange = 1.6f;
    // ※ 아래 시간들은 애니메이터가 실제로 해당 클립에 들어간 순간부터 센다 (BatAttackState 참고)
    [Tooltip("Bite Attack 클립(0.833s) 시작부터 입을 다무는 순간까지의 시간(초)")]
    public float BiteHitTime = 0.3f;
    [Tooltip("물기 모션 길이(초). 끝나면 Idle로 돌아간다. 클립 끝의 Exit 전환(0.25s)과 겹치도록 조금 짧게")]
    public float BiteDuration = 0.75f;
    [Tooltip("물기 판정 허용 각도(정면 기준 전체 각도)")]
    public float BiteAngle = 120f;

    [Header("외눈 박쥐 - 눈알 탄")]
    [Tooltip("투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "MischiefProjectile";
    [Tooltip("눈알 탄 내부 쿨타임(초). 물기와 별개로 돈다")]
    public float ProjectileCooldown = 3.5f;
    [Tooltip("Projectile Attack 클립(1.0s) 시작부터 탄이 나가는 순간까지의 시간(초)")]
    public float ProjectileReleaseTime = 0.4f;
    [Tooltip("발사 모션 길이(초). 이 동안 제자리에 고정된다")]
    public float ProjectileCastTime = 0.85f;
    [Tooltip("투사체 이동 속도")]
    public float ProjectileSpeed = 11f;
    [Tooltip("투사체 생존 시간(초). 초과 시 미명중으로 회수")]
    public float ProjectileLifeTime = 2.5f;
    [Tooltip("투사체 데미지. 0 이하면 AttackDamage 사용")]
    public float ProjectileDamage = 0f;
    [Tooltip("발사 높이(박쥐 본체 기준). 모델 눈 높이 1.1 + 비행 높이 hoverHeight 0.8")]
    public float MuzzleHeight = 1.9f;
    [Tooltip("조준 높이(플레이어 발 기준)")]
    public float AimHeight = 1f;
}

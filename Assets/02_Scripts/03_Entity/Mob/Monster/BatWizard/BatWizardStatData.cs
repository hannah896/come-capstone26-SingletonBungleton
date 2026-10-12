using UnityEngine;

/// <summary>
/// 외눈 박쥐 위저드(정예 원거리 마법사) 전용 스탯 SO.
/// 베이스 AttackRange는 "마법탄/스펠 사용 거리"(공격 상태 진입 거리)이고, 그 안에서 거리와 쿨타임으로 기술을 고른다.
/// 시간 값은 모두 애니메이터가 해당 클립에 실제로 들어간 순간부터 센다 (BatWizardAttackState 참고).
/// </summary>
[CreateAssetMenu(fileName = "BatWizardStatData", menuName = "Scriptable Objects/BatWizardStatData")]
public class BatWizardStatData : MonsterStatData
{
    [Header("위저드 - 공통")]
    [Tooltip("기술이 끝나고 다음 기술을 고르기 전까지 숨 돌리는 시간(초)")]
    public float SkillGap = 0.5f;

    [Header("위저드 - 근접 (Slash / Slice 번갈아)")]
    public float MeleeRange = 2.2f;
    public float MeleeDamage = 10f;
    [Tooltip("Slash/Slice 클립(0.667s) 시작부터 타격까지")]
    public float MeleeHitTime = 0.3f;
    public float MeleeDuration = 0.6f;
    [Tooltip("판정 허용 각도(정면 기준 전체 각도)")]
    public float MeleeAngle = 110f;
    public float MeleeCooldown = 1.2f;

    [Header("위저드 - 스핀 (주변 전원)")]
    public float SpinRadius = 2.5f;
    public float SpinDamage = 8f;
    [Tooltip("회전 지속 시간(초). Spin Attack 클립(0.333s 루프)을 이 시간 동안 반복")]
    public float SpinDuration = 1.0f;
    [Tooltip("이 시점들(클립 시작 기준, 초)에 범위 안의 플레이어 전원을 한 번씩 친다")]
    public float[] SpinHitTimes = { 0.35f, 0.8f };
    public float SpinKnockback = 8f;
    public float SpinKnockbackDuration = 0.25f;
    public float SpinCooldown = 7f;

    [Header("위저드 - 마법탄 (원거리)")]
    [Tooltip("투사체 프리팹의 Addressable 키")]
    public string ProjectileKey = "MischiefProjectile";
    public float ProjectileDamage = 9f;
    public float ProjectileSpeed = 13f;
    public float ProjectileLifeTime = 1.5f;
    [Tooltip("Projectile Attack 클립(0.667s) 시작부터 탄이 나가는 순간까지")]
    public float ProjectileReleaseTime = 0.3f;
    public float ProjectileDuration = 0.6f;
    [Tooltip("발사 높이(본체 기준)")]
    public float MuzzleHeight = 1.3f;
    [Tooltip("조준 높이(플레이어 발 기준)")]
    public float AimHeight = 1f;
    public float ProjectileCooldown = 2.5f;

    [Header("위저드 - 스펠 (추적 운석)")]
    [Tooltip("운석 장판 프리팹의 Addressable 키 (데빌과 같은 Meteor)")]
    public string MeteorKey = "Meteor";
    [Tooltip("타깃 발밑에 순서대로 떨어뜨리는 운석 수. 매번 그 순간의 타깃 위치를 노린다")]
    public int MeteorCount = 3;
    [Tooltip("운석 사이 간격(초)")]
    public float MeteorInterval = 0.5f;
    public float MeteorImpactRadius = 1.6f;
    public float MeteorWarningTime = 1.0f;
    public float MeteorLingerTime = 1.2f;
    public float MeteorDamage = 12f;
    [Tooltip("명중 시 이동 속도 배율(1 미만이면 둔화)")]
    public float MeteorSlowMultiplier = 0.6f;
    public float MeteorSlowDuration = 2f;
    [Tooltip("Cast Spell 클립(0.833s) 시작부터 첫 운석 장판이 깔리는 순간까지")]
    public float SpellCastPoint = 0.4f;
    public float SpellDuration = 0.8f;
    [Tooltip("이 거리보다 가까우면 스펠 대신 근접/스핀을 쓴다(제 발밑에 깔지 않게)")]
    public float SpellMinDistance = 4f;
    public float SpellCooldown = 9f;
}

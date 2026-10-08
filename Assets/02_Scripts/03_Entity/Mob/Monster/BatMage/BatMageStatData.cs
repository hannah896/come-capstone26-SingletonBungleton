using UnityEngine;

/// <summary>
/// 만월 보스 외눈 박쥐 메이지 전용 스탯 SO.
/// 베이스 AttackRange는 "스펠 사용 거리"(공격 상태 진입 거리)이고, 그 안에서 거리와 쿨타임으로 기술을 고른다.
/// 시간 값은 모두 애니메이터가 해당 클립에 실제로 들어간 순간부터 센다 (BatMageSkillState 참고).
/// </summary>
[CreateAssetMenu(fileName = "BatMageStatData", menuName = "Scriptable Objects/BatMageStatData")]
public class BatMageStatData : MonsterStatData
{
    [Header("메이지 - 공통")]
    [Tooltip("피격 경직(Hit) 최소 간격(초). 보스라 매번 끊기지 않고 이 간격으로만 경직된다")]
    public float StaggerCooldown = 5f;
    [Tooltip("기술 사이 최소 간격(초). 기술이 끝나고 다음 기술을 고르기 전까지 숨 돌리는 시간")]
    public float SkillGap = 0.6f;
    [Tooltip("이 체력 비율 이하부터 2페이즈 (스펠이 별 2개 연속으로 강화)")]
    public float Phase2HpRatio = 0.5f;

    [Header("메이지 - 슬래시 (단일 타깃)")]
    public float SlashRange = 2.8f;
    public float SlashDamage = 20f;
    [Tooltip("Slash Attack 클립(0.833s) 시작부터 타격까지")]
    public float SlashHitTime = 0.35f;
    public float SlashDuration = 0.8f;
    [Tooltip("판정 허용 각도(정면 기준 전체 각도)")]
    public float SlashAngle = 100f;
    public float SlashCooldown = 1.6f;

    [Header("메이지 - 스핀 (주변 전원)")]
    public float SpinRadius = 3.5f;
    [Tooltip("타격 1회당 데미지")]
    public float SpinDamage = 12f;
    [Tooltip("회전 지속 시간(초). Spin Attack 클립(0.333s 루프)을 이 시간 동안 반복")]
    public float SpinDuration = 1.8f;
    [Tooltip("이 시점들(클립 시작 기준, 초)에 범위 안의 플레이어 전원을 한 번씩 친다")]
    public float[] SpinHitTimes = { 0.4f, 1.0f, 1.6f };
    [Tooltip("마지막 타격에서 바깥으로 밀어내는 힘")]
    public float SpinKnockback = 12f;
    public float SpinKnockbackDuration = 0.3f;
    [Tooltip("회전하는 동안 타깃 쪽으로 다가가는 속도 배율(MoveSpeed 기준)")]
    public float SpinMoveMultiplier = 0.5f;
    public float SpinCooldown = 8f;

    [Header("메이지 - 스펠 (별 모양 마법 레이저)")]
    [Tooltip("별 레이저 장판 프리팹의 Addressable 키")]
    public string StarLaserKey = "MageStarLaser";
    [Tooltip("별(펜타그램) 바깥 꼭짓점까지의 반지름(m). 메이지 발밑이 중심")]
    public float StarRadius = 8f;
    [Tooltip("레이저 판정 폭(m)")]
    public float StarLaserWidth = 1.0f;
    public float StarDamage = 25f;
    [Tooltip("예고선이 보인 뒤 레이저가 터지기까지(초). 이 동안 선 밖으로 피하면 된다")]
    public float StarWarningTime = 1.2f;
    [Tooltip("레이저가 켜져 있는 시간(초). 판정은 켜지는 순간 한 번")]
    public float StarLaserTime = 0.6f;
    [Tooltip("Cast Spell 클립(1.0s) 시작부터 별이 깔리는 순간까지")]
    public float SpellCastPoint = 0.5f;
    public float SpellDuration = 1.0f;
    [Tooltip("2페이즈에서 두 번째 별이 깔리기까지의 간격(초). 첫 별과 36° 엇갈리게 깔린다")]
    public float Phase2SecondStarDelay = 0.9f;
    public float SpellCooldown = 10f;

    [Header("메이지 - 회복 (주변 아군 몬스터)")]
    [Tooltip("회복 연출(초록 원) 프리팹의 Addressable 키")]
    public string HealCircleKey = "MageHealCircle";
    [Tooltip("회복 반경(m). 메이지 발밑이 중심")]
    public float HealRadius = 10f;
    [Tooltip("이 체력 비율 이하인 아군이 반경 안에 있을 때만 시전한다")]
    public float HealTriggerHpRatio = 0.6f;
    [Tooltip("대상 최대 체력 대비 회복량 비율")]
    public float HealAmountRatio = 0.3f;
    [Tooltip("메이지 자신도 회복할지. 보스가 스스로 피를 채우면 전투가 늘어지기만 해서 기본은 끔")]
    public bool HealSelf = false;
    [Tooltip("Summon Attack 클립(1.0s) 시작부터 회복이 들어가는 순간까지")]
    public float HealCastPoint = 0.5f;
    public float HealDuration = 1.0f;
    public float HealCooldown = 15f;
}

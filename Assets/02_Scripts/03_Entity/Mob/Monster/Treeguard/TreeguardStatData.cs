using UnityEngine;

/// <summary>
/// 만월 보스 트리가드 전용 스탯 SO. (기획: docs/기획_트리가드_스킬로직.md)
/// 베이스 AttackRange가 "교전 사거리"이고, 그 안에서 7개 기술을 거리·방향·쿨타임으로 골라 쓴다.
/// 기술 하나는 "방향 고정 → 예고 → 타격 → 빈틈" 순서로 진행되며, 시간 값은 모두 기술 시작 기준(초)이다.
/// </summary>
[CreateAssetMenu(fileName = "TreeguardStatData", menuName = "Scriptable Objects/TreeguardStatData")]
public class TreeguardStatData : MonsterStatData
{
    [Header("트리가드 - 페이즈")]
    [Tooltip("이 HP 비율 아래로 내려가면 2페이즈 (숲의 분노)")]
    [Range(0f, 1f)] public float Phase2HpRatio = 0.6f;
    [Tooltip("이 HP 비율 아래로 내려가면 3페이즈 (고목의 최후)")]
    [Range(0f, 1f)] public float Phase3HpRatio = 0.3f;
    [Tooltip("2페이즈 걷기 속도. 1페이즈는 베이스 MoveSpeed")]
    public float Phase2MoveSpeed = 2.4f;
    [Tooltip("3페이즈 뛰기 속도")]
    public float Phase3RunSpeed = 4.5f;
    [Tooltip("3페이즈 쿨타임 배율 (0.7 = 30% 감소)")]
    public float Phase3CooldownMultiplier = 0.7f;
    [Tooltip("페이즈 전환 연출(Cast Spell) 동안 무적인 시간(초)")]
    public float PhaseShiftDuration = 2f;

    [Header("트리가드 - 기술 사이 최소 간격")]
    public float SkillGap = 0.8f;
    public float Phase3SkillGap = 0.5f;

    [Header("트리가드 - 강인도 / 그로기")]
    public float PoiseMax = 100f;
    [Tooltip("마지막 피격 후 이 시간(초)이 지나야 강인도가 회복되기 시작한다")]
    public float PoiseRegenDelay = 4f;
    public float PoiseRegenPerSecond = 15f;
    [Tooltip("강인도가 깨졌을 때 무방비로 서 있는 시간(초)")]
    public float GroggyDuration = 3f;
    [Tooltip("그로기 중 받는 데미지 배율")]
    public float GroggyDamageMultiplier = 1.5f;
    [Tooltip("그로기 연출 클립(Take Damage) 길이. 이후는 Idle로 서 있는다")]
    public float GroggyClipLength = 0.83f;

    [Header("트리가드 - 공통")]
    [Tooltip("장판 기술(내려찍기/대지 울림/박수/투척)이 쓰는 예고 장판 프리팹의 Addressable 키 (Meteor 컴포넌트)")]
    public string ZoneKey = "RootSnare";
    [Tooltip("근접 공격 클립 길이(초). 빈틈 동안 클립을 붙잡아 둘지 판단하는 기준")]
    public float MeleeClipLength = 1.17f;
    [Tooltip("장판 발동 후 이펙트가 남아 있는 시간(초)")]
    public float ZoneLingerTime = 1f;

    [Header("가지 휩쓸기 (Swing) - 전방 넓은 부채꼴, 뒤로 피한다")]
    public float SwingCooldown = 4f;
    public float SwingRange = 4f;
    [Range(0f, 360f)] public float SwingAngle = 180f;
    public float SwingHitTime = 0.6f;
    [Tooltip("기술 시작부터 다음 판단까지 걸리는 전체 시간(클립 + 빈틈)")]
    public float SwingDuration = 2.0f;
    public float SwingDamage = 18f;
    public float SwingKnockback = 10f;

    [Header("고목 내려찍기 (Smack) - 전방 원형 장판, 옆으로 피한다. 끝나고 팔이 박혀 있는 긴 빈틈")]
    public float SmackCooldown = 7f;
    [Tooltip("이 거리 안에 타깃이 있으면 내려찍기를 고른다")]
    public float SmackRange = 3.5f;
    [Tooltip("장판 중심이 몸 앞으로 떨어진 거리")]
    public float SmackForwardOffset = 3f;
    public float SmackRadius = 2.5f;
    [Tooltip("타격 시점 = 장판 예고 시간")]
    public float SmackHitTime = 0.9f;
    public float SmackDuration = 2.9f;
    public float SmackDamage = 35f;

    [Header("밀쳐내기 (Kick) - 바로 앞을 떼어낸다")]
    public float KickCooldown = 5f;
    public float KickRange = 2.5f;
    [Tooltip("이 거리 안에 붙어 있으면 밀쳐내기를 고른다")]
    public float KickTriggerRange = 1.8f;
    [Range(0f, 360f)] public float KickAngle = 60f;
    public float KickHitTime = 0.45f;
    public float KickDuration = 1.6f;
    public float KickDamage = 10f;
    public float KickKnockback = 16f;

    [Header("대지 울림 (Step) - 자기 주변 360°, 등 뒤 공략의 대가")]
    public float StepCooldown = 8f;
    [Tooltip("등 뒤 판정: 정면에서 이 각도의 절반을 벗어나면 등 뒤로 본다")]
    [Range(0f, 360f)] public float StepFrontAngle = 120f;
    [Tooltip("이 거리 안에서 등 뒤에 있으면 대지 울림을 고른다")]
    public float StepTriggerRange = 3f;
    public float StepRadius = 4.5f;
    public float StepHitTime = 0.7f;
    public float StepDuration = 2.3f;
    public float StepDamage = 15f;
    [Range(0f, 1f)] public float StepSlowMultiplier = 0.5f;
    public float StepSlowDuration = 2f;

    [Header("박수 충격파 (Clap) - 전방 직선으로 이어지는 장판, 옆으로 피한다")]
    public float ClapCooldown = 10f;
    [Tooltip("타깃이 이 거리 이상 떨어져 있을 때 고른다")]
    public float ClapMinRange = 6f;
    public int ClapWaveCount = 5;
    public float ClapWaveRadius = 1.5f;
    [Tooltip("장판 사이 간격(m). 개수 × 간격 ≒ 충격파 길이")]
    public float ClapWaveSpacing = 2f;
    [Tooltip("장판이 하나씩 이어지는 시간 간격(초)")]
    public float ClapWaveInterval = 0.15f;
    public float ClapHitTime = 0.6f;
    public float ClapDuration = 2.2f;
    public float ClapDamage = 25f;

    [Header("가지 투척 (Projectile) - 타깃 발밑 낙하 장판")]
    public float ProjectileCooldown = 5f;
    [Tooltip("타깃이 이 거리 이상 떨어져 있을 때 고른다")]
    public float ProjectileMinRange = 8f;
    public float ProjectileRadius = 2f;
    [Tooltip("장판 예고 시간 = 낙하까지 걸리는 시간")]
    public float ProjectileWarningTime = 1f;
    public float ProjectileDuration = 1.3f;
    public float ProjectileDamage = 15f;

    [Header("숲의 부름 (Cast Spell) - 트리앤트 미니언 소환")]
    [Tooltip("소환할 미니언의 Addressable 키. MonsterCatalog에 등록된 키여야 한다. 번갈아 소환한다")]
    public string[] MinionKeys = { "TreantMinionAutumn", "TreantMinionEvergreen" };
    public float SummonCooldown = 20f;
    public float Phase3SummonCooldown = 14f;
    [Tooltip("페이즈별 1회 소환 마릿수 (1/2/3페이즈)")]
    public int[] SummonCountPerPhase = { 2, 3, 4 };
    [Tooltip("살아있는 소환 미니언 상한")]
    public int MaxAliveMinions = 6;
    [Tooltip("살아있는 소환 미니언이 이 수보다 적을 때만 소환한다")]
    public int SummonWhenFewerThan = 2;
    [Tooltip("소환 위치: 보스 주변 원의 반지름")]
    public float SummonRadius = 5f;
    public float SummonHitTime = 0.6f;
    public float SummonDuration = 2f;

    [Header("3페이즈 연계 - 내려찍기 직후 휩쓸기")]
    [Range(0f, 1f)] public float Phase3ComboChance = 0.5f;
    [Tooltip("연계 휩쓸기의 전체 시간. 연계 뒤 빈틈을 늘려 보상을 지킨다")]
    public float ComboSwingDuration = 3.1f;
}

using UnityEngine;

/// <summary>
/// 최종 보스 드래곤(서리룡/망령룡) 전용 스탯 SO. (기획: docs/기획_최종보스_월식룡.md)
/// 시간 값은 모두 애니메이터가 해당 클립에 실제로 들어간 순간부터 센다.
/// 거리·높이 값은 모델 스케일 1.0(서리룡 키 ~5.6m, 길이 ~12.7m, 날개폭 ~19m) 기준이다.
/// </summary>
[CreateAssetMenu(fileName = "DragonStatData", menuName = "Scriptable Objects/DragonStatData")]
public class DragonStatData : MonsterStatData
{
    [Header("드래곤 - 보스 체력바 (UI_Hud_BossHealth)")]
    [Tooltip("체력바 위에 표시할 보스 이름")]
    public string BossName = "서리달의 수호룡";
    [Tooltip("체력을 몇 줄로 나눠 보여줄지 (×N). 연출용이며 실제 체력은 MaxHP")]
    public int HpLineCount = 30;
    [Tooltip("줄마다 돌아가며 쓰는 색. 한 줄을 깎으면 다음 색이 드러난다")]
    public Color[] HpLineColors =
    {
        new Color(0.55f, 0.85f, 1f),
        new Color(0.85f, 0.95f, 1f),
        new Color(0.35f, 0.6f, 0.95f),
        new Color(0.6f, 0.95f, 0.9f),
    };

    [Header("드래곤 - 공통")]
    [Tooltip("패턴과 패턴 사이 최소 간격(초)")]
    public float SkillGap = 1.2f;
    [Tooltip("이 거리 안이면 걸어 다가가지 않고 제자리에서 몸만 돌린다")]
    public float StopDistance = 7.5f;
    [Tooltip("아레나 반경(m). 돌진·이동이 이 밖으로 나가지 않는다 (지름 70m 아레나 기준)")]
    public float ArenaRadius = 32f;
    [Tooltip("패턴 시작 전 타깃 쪽으로 몸을 돌리는 속도(도/초). 평소 회전은 베이스 TurnSpeed")]
    public float AimTurnSpeed = 240f;

    [Header("드래곤 - 무력화(그로기)")]
    [Tooltip("무력화 게이지 최대치. 받은 피해만큼 깎이고 0이 되면 그로기")]
    public float PoiseMax = 600f;
    [Tooltip("머리에 맞으면 무력화 피해 배율")]
    public float HeadPoiseMultiplier = 2f;
    [Tooltip("마지막 피격 후 이 시간이 지나면 무력화 게이지가 회복되기 시작한다")]
    public float PoiseRegenDelay = 6f;
    public float PoiseRegenPerSecond = 15f;
    [Tooltip("무력화 그로기 지속 시간(초)")]
    public float GroggyDuration = 8f;
    [Tooltip("무력화 그로기 중 받는 피해 배율")]
    public float GroggyDamageMultiplier = 1.5f;
    [Tooltip("머리 판정 반경(m). 피격 지점이 머리 뼈에서 이 거리 안이면 머리 명중")]
    public float HeadHitRadius = 1.6f;

    [Header("드래곤 - 카운터")]
    [Tooltip("카운터 성공 시 깎는 무력화 게이지")]
    public float CounterPoiseDamage = 200f;
    [Tooltip("카운터 성공 시 경직(GetHit2) 시간(초)")]
    public float CounterStaggerDuration = 1.6f;
    [Tooltip("머리가 아니어도, 이 거리 안 정면(±CounterFrontAngle/2)에서 친 공격은 카운터로 인정한다 (멀티 클라 보고는 피격 지점 대신 공격자 위치가 오므로)")]
    public float CounterFrontReach = 10f;
    public float CounterFrontAngle = 70f;

    [Header("드래곤 - 서리 물기 ★")]
    public float BiteRange = 8f;
    [Tooltip("정면 부채꼴 전체 각도")]
    public float BiteAngle = 60f;
    public float BiteDamage = 45f;
    [Tooltip("BiteAttack 클립(1.33s) 시작부터 타격까지. 이 시간 동안 머리가 파랗게 빛난다(카운터 구간)")]
    public float BiteHitTime = 0.55f;
    public float BiteDuration = 1.33f;
    public float BiteKnockback = 10f;
    public float BiteCooldown = 4f;

    [Header("드래곤 - 빙탄 3연사")]
    [Tooltip("얼음탄 프리팹 Addressable 키")]
    public string FrostBallKey = "DragonFrostBall";
    [Tooltip("얼음 장판 프리팹 Addressable 키")]
    public string IcePatchKey = "DragonIcePatch";
    public int SpitCount = 3;
    [Tooltip("SpitFrozenBall 클립(1.0s) 시작부터 탄이 입을 떠나는 순간까지")]
    public float SpitReleaseTime = 0.45f;
    [Tooltip("한 발 당 클립 시간. 이 간격으로 다음 탄을 쏜다")]
    public float SpitInterval = 1.0f;
    [Tooltip("탄이 착탄 지점까지 날아가는 시간(초). 착탄 지점에 그동안 예고 원이 뜬다")]
    public float SpitFlightTime = 0.9f;
    public float SpitImpactRadius = 2.2f;
    public float SpitDamage = 25f;
    [Tooltip("장판 반경/지속/둔화 배율(밟는 동안). 지속이 0 이하면 페이즈 내내 남는다 (2페이즈 산성 웅덩이)")]
    public float IcePatchRadius = 2.6f;
    public float IcePatchDuration = 6f;
    public float IcePatchSlow = 0.5f;
    [Tooltip("장판 위에 서 있는 동안 초당 피해 (2페이즈 산성 웅덩이)")]
    public float PatchDotPerSecond = 0f;
    public float SpitMinRange = 7f;
    public float SpitCooldown = 9f;

    [Header("드래곤 - 서리 브레스")]
    [Tooltip("브레스 사거리(m)")]
    public float BreathRange = 14f;
    [Tooltip("판정 원뿔 반각(도). 입 방향 기준 ±이 각도 안에 있으면 맞는다")]
    public float BreathHalfAngle = 15f;
    [Tooltip("SpreadFrozenBreath 클립(3.33s)에서 실제로 뿜는 구간(초)")]
    public float BreathStartTime = 1.0f;
    public float BreathEndTime = 2.85f;
    public float BreathDuration = 3.33f;
    [Tooltip("뿜는 동안 몸통을 좌→우로 돌리는 전체 각도. 클립 자체의 ±15°와 합쳐 약 120° 부채꼴이 된다")]
    public float BreathSweepAngle = 90f;
    [Tooltip("판정 틱 간격(초)과 틱 데미지")]
    public float BreathTickInterval = 0.2f;
    public float BreathTickDamage = 8f;
    [Tooltip("틱마다 쌓이는 빙결 게이지(가득=1). 가득 차면 빙결")]
    public float FreezePerTick = 0.25f;
    [Tooltip("브레스를 안 맞는 동안 빙결 게이지가 초당 줄어드는 양")]
    public float FreezeDecayPerSecond = 0.25f;
    public float FreezeDuration = 2f;
    public float BreathCooldown = 14f;

    [Header("드래곤 - 얼음 돌진")]
    [Tooltip("돌진 경로 예고 시간(초)")]
    public float ChargeWarningTime = 1.0f;
    public float ChargeSpeed = 9f;
    [Tooltip("최대 돌진 거리(m). 타깃까지 거리 + ChargeOvershoot 와 이 값 중 작은 쪽")]
    public float ChargeMaxDistance = 28f;
    public float ChargeOvershoot = 4f;
    [Tooltip("돌진 판정 폭(m, 전체)")]
    public float ChargeWidth = 4.5f;
    public float ChargeDamage = 40f;
    public float ChargeKnockback = 16f;
    [Tooltip("기둥(DragonCover)에 부딪히면 스스로 그로기에 빠지는 시간(초)")]
    public float ChargeStunDuration = 3f;
    public float ChargeMinRange = 10f;
    public float ChargeCooldown = 12f;

    [Header("드래곤 - 비행")]
    [Tooltip("체공 고도(m, 루트 기준). 5.1m 이상이어야 추락 클립이 땅을 뚫지 않는다")]
    public float FlyHeight = 7f;
    [Tooltip("TakeOff 클립(1.83s) 중 고도를 올리기 시작/끝내는 시간")]
    public float TakeOffRiseStart = 0.4f;
    public float TakeOffDuration = 1.83f;
    [Tooltip("착지 클립(2.17s) 중 고도를 땅까지 내리는 데 쓰는 시간")]
    public float LandDescendTime = 1.6f;
    public float LandDuration = 2.17f;
    [Tooltip("이 체력 비율 아래로 내려갈 때마다 이륙해 공중 페이즈를 시작한다 (×30줄 중 ×20, ×10)")]
    public float[] TakeOffHpRatios = { 0.667f, 0.333f };

    [Header("드래곤 - 공중 페이즈 · 격추")]
    [Tooltip("격추 게이지(공중에서 받은 피해만큼 깎인다)")]
    public float ShootdownMax = 300f;
    [Tooltip("격추 제한 시간(초). 넘기면 활공 후 서리 폭발")]
    public float ShootdownTimeLimit = 25f;
    [Tooltip("공중 패턴 사이 간격(초)")]
    public float AirSkillGap = 1.5f;
    [Tooltip("공중 빙탄 폭격: 플레이어마다 발밑 예고 원 → 착탄. 반복 횟수/간격")]
    public int BombardCount = 6;
    public float BombardInterval = 0.8f;
    [Tooltip("FlyStationarySpitFrozenBall 클립(0.8s) 시작부터 탄이 입을 떠나는 순간까지")]
    public float FlySpitReleaseTime = 0.35f;
    public float BombardFlightTime = 1.2f;
    [Tooltip("공중 폭격이 남기는 장판 지속 시간(초). 0 이하면 영구 — 폭격 장판까지 영구면 아레나가 금방 덮이므로 따로 둔다")]
    public float BombardPatchDuration = 6f;
    [Tooltip("공중 브레스 (FlyStationarySpread…Breath 3.2s) 뿜는 구간과 몸통 회전 각도")]
    public float FlyBreathStartTime = 0.6f;
    public float FlyBreathEndTime = 2.9f;
    public float FlyBreathDuration = 3.2f;
    public float FlyBreathSweepAngle = 110f;
    public float FlyBreathRange = 22f;

    [Header("드래곤 - 격추 성공 (추락 → 그로기)")]
    [Tooltip("FlyStationaryToFalling 클립(1.33s). 이 동안 몸이 위로 솟는 만큼 루트를 내려 몸 높이를 유지한다")]
    public float FallStartDuration = 1.33f;
    public float FallStartDrop = 5.1f;
    [Tooltip("Falling 중 낙하 가속도(m/s²)")]
    public float FallGravity = 30f;
    [Tooltip("DeathHitTheGround 클립(0.67s) 시작부터 땅에 부딪히는 순간까지")]
    public float CrashImpactTime = 0.2f;
    public float CrashDuration = 0.67f;
    [Tooltip("땅에 부딪힐 때 주변 넉백 (피해 없음)")]
    public float CrashKnockbackRadius = 8f;
    public float CrashKnockback = 12f;
    public float ShootdownGroggyDuration = 10f;
    public float ShootdownDamageMultiplier = 2f;

    [Header("드래곤 - 격추 실패 (활공 → 서리 폭발 전멸기)")]
    [Tooltip("아레나 중심 주위를 도는 활공 반경(m)과 한 바퀴 시간(초)")]
    public float GlideRadius = 20f;
    public float GlideLapTime = 8f;
    [Tooltip("착지 후 폭발까지 예고 시간(초)")]
    public float ExplosionWarningTime = 2.5f;
    [Tooltip("최대 체력 대비 피해 비율. 엄폐(DragonCover 안전 반경) 안이면 SafeRatio")]
    public float ExplosionHpRatio = 0.8f;
    public float ExplosionSafeHpRatio = 0.5f;
    public float ExplosionRadius = 40f;

    [Header("2페이즈 - 부식 (망령룡)")]
    [Tooltip("부식 물기에 맞은 플레이어가 이 보스에게 받는 피해 배율. 1이면 부식 없음")]
    public float BiteCorrosionMultiplier = 1f;
    public float BiteCorrosionDuration = 10f;
    [Tooltip("브레스가 기둥(DragonCover)을 부식시키는지. 한 번의 브레스에 기둥당 1회")]
    public bool BreathCorrodesCover = false;
    [Tooltip("돌진 경로에 장판을 남기는 간격(m). 0이면 남기지 않는다")]
    public float ChargeTrailSpacing = 0f;

    [Header("2페이즈 - 격추 실패 시 월식 폭우 (FailMode = AcidRain)")]
    public DragonShootdownFail ShootdownFailMode = DragonShootdownFail.FrostExplosion;
    [Tooltip("폭우 지속 시간(초). 살아 있는 기둥 안전지대 밖이면 틱마다 피해")]
    public float AcidRainDuration = 8f;
    public float AcidRainTickInterval = 0.5f;
    public float AcidRainTickDamage = 10f;

    [Header("2페이즈 - 최종 발악 (×5줄 이하)")]
    [Tooltip("이 체력 비율 이하가 되면 중앙에서 브레스를 360° 돌리며 걷는다. 0이면 쓰지 않음")]
    public float FrenzyHpRatio = 0f;
    [Tooltip("이 시간 안에 못 잡으면 광폭화 (전원에게 최대 체력 비율 피해)")]
    public float FrenzyDuration = 15f;
    public float FrenzyTurnSpeed = 40f;
    public float FrenzyWalkSpeed = 1.2f;
    public float FrenzyRange = 16f;
    public float EnrageHpRatio = 2f;

    [Header("등장 연출 (페이즈 전환으로 등장할 때)")]
    [Tooltip("이륙해서 포효하는 시간(초) — 이후 착지")]
    public float IntroHoverTime = 2.5f;
    public float IntroShockwaveRadius = 10f;
    public float IntroShockwaveKnockback = 14f;
}

/// <summary>격추 실패 시 전멸기 종류.</summary>
public enum DragonShootdownFail
{
    /// <summary>1페이즈: 활공 → 착지 → 서리 폭발 (기둥 옆이면 감소).</summary>
    FrostExplosion,
    /// <summary>2페이즈: 활공 → 착지 → 월식 폭우 (기둥 옆만 안전).</summary>
    AcidRain,
}

using UnityEngine;

public enum PeriodicMonsterPositionMode
{
    PlayerRing,
    FixedWorldPoint
}

/// <summary>월드 시계의 누적 게임 시간에 따라 반복해서 실행할 규칙입니다.</summary>
[CreateAssetMenu(fileName = "PeriodicSpawnRule", menuName = "Scriptable Objects/TestWorld/PeriodicSpawnRule")]
public sealed class PeriodicSpawnRule : ScriptableObject
{
    [Header("스폰 대상과 일정")]
    [Tooltip("저장 시 일정을 식별하는 고유 ID. 에셋 이름 변경·목록 순서 변경 후에도 유지하세요.")]
    public string ruleId;

    [Tooltip("MonsterCatalog에 등록된 몬스터의 Addressable 키입니다.")]
    public string monsterKey;

    [Tooltip("월드 시계의 게임 시간 기준 간격(초)입니다.")]
    [Min(0.1f)] public float intervalSeconds = 300f;

    [Tooltip("새 월드가 시작된 뒤 첫 스폰 검사까지 기다릴 게임 시간(초)입니다. 저장된 월드에서는 다음 검사 시간을 복원합니다.")]
    [Min(0f)] public float firstDelaySeconds = 300f;

    [Tooltip("몬스터 생성을 허용할 월드 시계의 시간대입니다.")]
    public SpawnTimePhaseMask allowedTimePhases = SpawnTimePhaseMask.All;

    [Header("생성 수량")]
    [Tooltip("한 번의 검사에서 생성할 최소 몬스터 수입니다.")]
    [Min(0)] public int minSpawnCount = 1;

    [Tooltip("한 번의 검사에서 생성할 최대 몬스터 수입니다.")]
    [Min(0)] public int maxSpawnCount = 1;

    [Tooltip("이 주기 규칙이 유지할 최대 몬스터 수입니다. 멀리 있어 일시적으로 비활성화된 개체도 포함합니다.")]
    [Min(0)] public int maxAlive = 10;

    [Header("생성 위치")]
    [Tooltip("PlayerRing은 무작위로 선택한 플레이어 주변, FixedWorldPoint는 지정한 월드 좌표 주변에 생성합니다.")]
    public PeriodicMonsterPositionMode positionMode = PeriodicMonsterPositionMode.PlayerRing;

    [Tooltip("PlayerRing에서 선택된 기준 플레이어와 몬스터 생성 위치 사이의 최소 거리(m)입니다.")]
    [Min(0f)] public float minDistanceFromPlayer = 12f;

    [Tooltip("PlayerRing에서 선택된 기준 플레이어와 몬스터 생성 위치 사이의 최대 거리(m)입니다.")]
    [Min(0f)] public float maxDistanceFromPlayer = 25f;

    [Tooltip("FixedWorldPoint에서 몬스터 생성 위치를 탐색할 중심 월드 좌표입니다.")]
    public Vector3 fixedWorldPoint;

    [Tooltip("FixedWorldPoint에서 중심 좌표 주변의 몬스터 생성 위치를 탐색할 반경(m)입니다.")]
    [Min(0f)] public float spawnRadius = 5f;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(ruleId)) ruleId = System.Guid.NewGuid().ToString("N");
    }
}

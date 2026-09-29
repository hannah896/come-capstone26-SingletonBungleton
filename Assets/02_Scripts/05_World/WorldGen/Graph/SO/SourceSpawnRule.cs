using UnityEngine;

/// <summary>월드에 배치된 집·둥지 등의 스폰 오브젝트가 사용하는 규칙입니다.</summary>
[CreateAssetMenu(fileName = "SourceSpawnRule", menuName = "Scriptable Objects/TestWorld/SourceSpawnRule")]
public sealed class SourceSpawnRule : ScriptableObject
{
    [Header("스폰 대상과 조건")]
    [Tooltip("MonsterCatalog에 등록된 몬스터의 Addressable 키입니다.")]
    public string monsterKey;

    [Tooltip("몬스터 생성을 허용할 월드 시계의 시간대입니다.")]
    public SpawnTimePhaseMask allowedTimePhases = SpawnTimePhaseMask.All;

    [Tooltip("스폰 검사 한 번이 성공할 확률입니다. 0이면 생성하지 않습니다.")]
    [Range(0f, 1f)] public float spawnChance = 1f;

    [Header("생성 수량과 위치")]
    [Tooltip("검사에 성공했을 때 한 번에 생성할 최소 몬스터 수입니다.")]
    [Min(0)] public int minSpawnCount = 1;

    [Tooltip("검사에 성공했을 때 한 번에 생성할 최대 몬스터 수입니다.")]
    [Min(0)] public int maxSpawnCount = 1;

    [Tooltip("이 스폰 오브젝트가 유지할 최대 몬스터 수입니다. 멀리 있어 일시적으로 비활성화된 개체도 포함합니다.")]
    [Min(0)] public int maxAlive = 3;

    [Tooltip("스폰 오브젝트 중심에서 몬스터 생성 위치를 탐색할 최대 반경(m)입니다.")]
    [Min(0f)] public float spawnRadius = 5f;

    [Tooltip("이 거리 안에 살아 있는 플레이어가 있을 때만 검사합니다. 0이면 월드 공통 정리 거리를 사용합니다.")]
    [Min(0f)] public float activationDistance = 60f;

    [Header("검사 주기")]
    [Tooltip("월드 시계의 게임 시간 기준 스폰 검사 간격(초)입니다.")]
    [Min(0.1f)] public float intervalSeconds = 30f;

    [Tooltip("새 월드에서 첫 스폰 검사를 즉시 시작할지 지정합니다. 저장된 월드에서는 다음 검사 시간을 복원합니다.")]
    public bool spawnImmediately = true;

    [Header("월드 배치")]
    [Tooltip("스폰 오브젝트를 배치할 때 필요한 빈 공간의 반경(m)입니다. 몬스터 생성 위치는 이 거리 바깥부터 찾되, spawnRadius가 더 작으면 그 반경을 사용합니다.")]
    [Min(0f)] public float placementRadius = 3f;

    [Tooltip("배치 중심과 주변 타일 사이에 허용할 최대 높이 차이(m)입니다.")]
    [Min(0f)] public float maxHeightDifference = 2f;

    [Tooltip("월드 시작 위치에서 스폰 오브젝트까지 확보할 최소 거리(m)입니다.")]
    [Min(0f)] public float minDistanceFromWorldStart = 30f;
}

using UnityEngine;

/// <summary>새 월드를 만들 때 바이옴의 각 지역에 한 번 배치할 몬스터 규칙입니다.</summary>
[CreateAssetMenu(fileName = "InitialSpawnRule", menuName = "Scriptable Objects/TestWorld/InitialSpawnRule")]
public sealed class InitialSpawnRule : ScriptableObject
{
    [Header("스폰 대상")]
    [Tooltip("MonsterCatalog에 등록된 몬스터의 Addressable 키입니다.")]
    public string monsterKey;

    [Header("월드 최초 배치")]
    [Tooltip("바이옴 지역 하나에 배치할 몬스터의 최소 목표 수입니다. 빈 타일이 부족하면 실제 배치 수는 줄어들 수 있습니다.")]
    [Min(0)] public int minCountPerRegion = 0;

    [Tooltip("바이옴 지역 하나에 배치할 몬스터의 최대 목표 수입니다.")]
    [Min(0)] public int maxCountPerRegion = 1;

    [Tooltip("같은 지역에서 이 규칙의 몬스터 배치 후보 사이에 확보할 최소 거리(m)입니다.")]
    [Min(0f)] public float minPlacementDistance = 4f;

    [Tooltip("월드 시작 위치에서 최초 배치 몬스터까지 확보할 최소 거리(m)입니다.")]
    [Min(0f)] public float minDistanceFromWorldStart = 25f;
}

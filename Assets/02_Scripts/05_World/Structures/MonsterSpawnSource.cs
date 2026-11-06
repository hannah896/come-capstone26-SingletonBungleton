using System;
using UnityEngine;

/// <summary>집·둥지 프리팹의 몬스터 생성 규칙과 배치 조건을 보관합니다. 실제 생성과 저장은 월드 디렉터가 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawnSource : MonoBehaviour
{
    [Tooltip("이 집·둥지가 생성할 몬스터와 생성 조건입니다.")]
    public SourceSpawnRule spawnRule;

    [Tooltip("이 프리팹을 월드에 배치할 때 사용하는 조건입니다.")]
    public SpawnerPlacementSettings placementSettings = new SpawnerPlacementSettings();

    /// <summary>프리팹 설정과 독립된 배치 조건의 사본을 반환합니다. 생성 규칙이 없으면 등록하지 않습니다.</summary>
    public MonsterSpawnSourceDefinition GetDefinition()
    {
        return spawnRule == null ? null : new MonsterSpawnSourceDefinition(spawnRule, placementSettings);
    }
}

/// <summary>스폰 오브젝트 프리팹이 사용하는 월드 배치 조건입니다.</summary>
[Serializable]
public sealed class SpawnerPlacementSettings
{
    [Tooltip("프리팹 배치에 필요한 빈 공간과 런타임 점유 영역의 반경(m)입니다. 몬스터 생성 위치는 이 거리 바깥부터 찾되, 생성 규칙의 spawnRadius가 더 작으면 그 반경을 사용합니다.")]
    [Min(0f)] public float placementRadius = 3f;

    [Tooltip("배치 중심과 주변 타일 사이에 허용할 최대 높이 차이(m)입니다.")]
    [Min(0f)] public float maxHeightDifference = 2f;

    [Tooltip("월드 시작 위치에서 이 스폰 오브젝트까지 확보할 최소 거리(m)입니다.")]
    [Min(0f)] public float minDistanceFromWorldStart = 30f;

    public SpawnerPlacementSettings Clone()
    {
        return new SpawnerPlacementSettings
        {
            placementRadius = Mathf.Max(0f, placementRadius),
            maxHeightDifference = Mathf.Max(0f, maxHeightDifference),
            minDistanceFromWorldStart = Mathf.Max(0f, minDistanceFromWorldStart)
        };
    }
}

/// <summary>월드 생성과 런타임 등록에 전달하는 프리팹 설정입니다.</summary>
public sealed class MonsterSpawnSourceDefinition
{
    public SourceSpawnRule SpawnRule { get; }
    public SpawnerPlacementSettings PlacementSettings { get; }

    public MonsterSpawnSourceDefinition(SourceSpawnRule spawnRule, SpawnerPlacementSettings placementSettings)
    {
        SpawnRule = spawnRule;
        PlacementSettings = placementSettings != null ? placementSettings.Clone() : new SpawnerPlacementSettings();
    }
}

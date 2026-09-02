using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnRuleSet", menuName = "Scriptable Objects/TestWorld/SpawnRuleSet")]
public class SpawnRuleSet : ScriptableObject
{
    [Header("플레이어 기준 몬스터 스폰 규칙")]
    [Tooltip("스폰 시점에 플레이어의 현재 좌표, 시간대를 검사합니다. 각 규칙은 하나의 몬스터 종류를 담당합니다.")]
    [SerializeField] public List<MonsterSpawnRule> MonsterRules = new();
}

/// <summary>
/// 플레이어 좌표를 기준으로 한 종류의 몬스터를 스폰하기 위한 런타임 규칙입니다.
/// monsterKey는 MonsterCatalog에 등록된 Addressable 키와 일치해야 합니다.
/// </summary>
[CreateAssetMenu(fileName = "MonsterSpawnRule", menuName = "Scriptable Objects/TestWorld/MonsterSpawnRule")]
public class MonsterSpawnRule : ScriptableObject
{
    [Header("대상")]
    [Tooltip("인스펙터에서 규칙을 구분하기 위한 이름입니다.")]
    public string ruleName;

    [Tooltip("MonsterCatalog에 등록된 몬스터의 Addressable 키입니다.")]
    public string monsterKey;

    [Header("플레이어 기준 스폰 위치")]
    [Tooltip("플레이어와 몬스터 사이에 반드시 확보할 최소 거리(m)입니다.")]
    [Min(0f)] public float minDistanceFromPlayer = 12f;

    [Tooltip("플레이어를 중심으로 스폰 위치를 탐색할 최대 거리(m)입니다.")]
    [Min(0f)] public float maxDistanceFromPlayer = 25f;

    [Header("시간대 조건")]     
    [Tooltip("스폰을 허용할 시간대입니다.")]
    public SpawnTimePhaseMask allowedTimePhases = SpawnTimePhaseMask.All;

    [Header("스폰 빈도 및 수량")]
    [Tooltip("이 규칙의 스폰 여부를 검사하는 최소 간격(초)입니다.")]
    [Min(0f)] public float spawnInterval = 10f;

    [Header("체류 가능한 시간")]  
    [Tooltip("스폰된 몬스터가 플레이어 정리 거리 밖에 체류할 수 있는 최대 시간(초)입니다.")]
    [Min(0f)] public float stayDuration = 30f;

    [Tooltip("스폰 검사 한 번이 성공할 확률입니다.")]
    [Range(0f, 1f)] public float spawnChance = 1f;

    [Tooltip("한 번의 스폰 성공 시 생성할 최소 몬스터 수입니다.")]
    [Min(0)] public int minSpawnCount = 1;

    [Tooltip("한 번의 스폰 성공 시 생성할 최대 몬스터 수입니다.")]
    [Min(0)] public int maxSpawnCount = 1;

    [Header("개체 수 제한")] // TODO: 몬스터 데이터로 옮기기
    [Tooltip("몬스터 한 마리가 소비하는 스폰 슬롯 비용")]
    [Min(0)] public int slotCost = 1;
}

/// <summary>월드 시계의 시간대를 스폰 조건으로 조합하기 위한 플래그입니다.</summary>
[Flags]
public enum SpawnTimePhaseMask
{
    None = 0,
    Dawn = 1 << 0,
    Morning = 1 << 1,
    Afternoon = 1 << 2,
    Dusk = 1 << 3,
    All = Dawn | Morning | Afternoon | Dusk
}

public static class SpawnTimePhaseMaskExtensions
{
    public static SpawnTimePhaseMask ToSpawnMask(this TimePhase timePhase)
    {
        return timePhase switch
        {
            TimePhase.Dawn => SpawnTimePhaseMask.Dawn,
            TimePhase.Morning => SpawnTimePhaseMask.Morning,
            TimePhase.Afternoon => SpawnTimePhaseMask.Afternoon,
            TimePhase.Dusk => SpawnTimePhaseMask.Dusk,
            _ => SpawnTimePhaseMask.None
        };
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>시드로 재생성한 배치에 적용할 몬스터 스폰 상태 변경분입니다.</summary>
[Serializable]
public sealed class MonsterSpawnSaveData
{
    public List<InitialMonsterSaveData> initialMonsters = new();
    public List<SpawnSourceSaveData> sources = new();
    public List<PeriodicSpawnSaveData> periodic = new();
    public List<SpawnedMonsterSaveData> members = new();
}

[Serializable]
public sealed class InitialMonsterSaveData
{
    public int id;
    public bool dead;
    public bool hasLiveState;
    public Vector3 position;
    public float hp;
}

[Serializable]
public sealed class SpawnSourceSaveData
{
    public int id;
    public float nextSpawnTime;
}

[Serializable]
public sealed class PeriodicSpawnSaveData
{
    public string ruleId;
    public float nextSpawnTime;
}

[Serializable]
public sealed class SpawnedMonsterSaveData
{
    public int sourceId;
    public string periodicRuleId;
    public Vector3 position;
    public float hp;
}

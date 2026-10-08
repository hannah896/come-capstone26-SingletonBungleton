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
    public int lastFullMoonDay;
    public int pendingFullMoonDay;
    /// <summary>직전 회차에 고른 몬스터 키 (후보가 여러 개인 규칙에서 연속 중복을 피하는 데 사용). 예전 세이브는 비어 있음.</summary>
    public string lastMonsterKey;
}

[Serializable]
public sealed class SpawnedMonsterSaveData
{
    public int sourceId;
    public string periodicRuleId;
    public Vector3 position;
    public float hp;
    /// <summary>실제로 스폰된 몬스터 키. 후보가 여러 개인 규칙에서 어떤 몬스터였는지 복원하는 데 사용. 예전 세이브는 비어 있음(규칙 기본 키 사용).</summary>
    public string monsterKey;
}

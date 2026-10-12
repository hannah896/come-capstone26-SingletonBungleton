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
    // 이전 저장 파일에서는 보스 주기 상태가 없으므로 null을 허용합니다.
    public BossSpawnSaveData boss;
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
    // 기존 만월 주기 룰의 저장 상태를 보스 상태로 이전하기 위해 유지합니다.
    public int lastFullMoonDay;
    public int pendingFullMoonDay;
}

[Serializable]
public sealed class BossSpawnSaveData
{
    public string ruleId;
    public int lastFullMoonDay;
    public int pendingFullMoonDay;
}

[Serializable]
public sealed class SpawnedMonsterSaveData
{
    public int sourceId;
    public string periodicRuleId;
    public string bossRuleId;
    // 보스는 현재 달 위상과 무관하게 저장 당시의 종류를 복원합니다.
    public string monsterKey;
    public Vector3 position;
    public float hp;
}

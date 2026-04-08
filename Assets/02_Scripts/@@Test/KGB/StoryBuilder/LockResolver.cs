using Firebase.Analytics;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "어떤 잠금(Lock)이 어떤 열쇠(Key)들로 풀리는가?"를 정의하는 클래스
/// </summary>
public static class LockResolver
{
    // Key: 잠금 ID (예: LOCK_THICKET)
    // Value: 해제 가능한 열쇠 ID 목록 (예: [KEY_AXE, KEY_FIRE])
    private static readonly Dictionary<string, List<string>> _lockRules = new()
    {
        { Locks.LOCK_NONE,    new List<string> { } }, // 잠금 없음

        { Locks.LOCK_TIER1,   new List<string> { Keys.KEY_TIER1 } },
   
        { Locks.LOCK_TIER2,   new List<string> { Keys.KEY_TIER2 } },
   
        { Locks.LOCK_TIER3,   new List<string> { Keys.KEY_TIER3 } },
   
        { Locks.LOCK_TIER4,   new List<string> { Keys.KEY_TIER4 } },

        { Locks.LOCK_TIER5,   new List<string> { Keys.KEY_TIER5 } },
    };

    /// <summary>
    /// 생성된 열쇠들로 특정 잠금(LockID)을 풀 수 있는지 확인
    /// </summary>
    public static bool CanUnlock(string lockID, List<string> availableKeys)
    {
        // 1. 등록되지 않은 잠금은 "조건 없음"으로 간주하여 통과 (또는 false로 막을 수도 있음)
        if (!_lockRules.ContainsKey(lockID))
        {
            Debug.LogWarning($"[LockResolver] 정의되지 않은 잠금 ID입니다: {lockID}");
            return true;
        }

        // 2. 필요한 열쇠 목록 가져오기
        var validKeys = _lockRules[lockID];

        // 3. 플레이어가 가진 열쇠 중 하나라도 일치하면 성공
        foreach (var key in validKeys)
        {
            if (availableKeys.Contains(key)) return true;
        }

        return false;
    }
}

// 환경적 장애물들 중심으로
public static class Locks
{
    public const string LOCK_NONE = "LOCK_NONE";
    public const string LOCK_TIER1 = "LOCK_TIER1";
    public const string LOCK_TIER2 = "LOCK_TIER2";
    public const string LOCK_TIER3 = "LOCK_TIER3";
    public const string LOCK_TIER4 = "LOCK_TIER4";
    public const string LOCK_TIER5 = "LOCK_TIER5";
}

//지역에서 획득(혹은 제작) 가능한 아이템을 중심으로(몬스터의 경우 몬스터를 처치하고 얻는 것들을 의미)
public static class Keys
{ 
    public const string KEY_TIER1 = "KEY_TIER1";
    public const string KEY_TIER2 = "KEY_TIER2";
    public const string KEY_TIER3 = "KEY_TIER3";
    public const string KEY_TIER4 = "KEY_TIER4";
    public const string KEY_TIER5 = "KEY_TIER5";
}


/// <summary>
///TODO: 참고용
//tasks = {
//              "Make a pick",              NONE, {
//				"Dig that rock",            ROCK
//				"Great Plains",             COMBAT<GOLD, HONEY> {
//				"Squeltch",                 SPIDERS, TIER2
//				"Beeeees!",                 BEEHIVE<AXE> ,TIER1
//				"Speak to the king",        TIER2
//				"Tentacle-Blocked The Deep Forest", TIER3
//		},
//		numoptionaltasks = 4,x  
//		optionaltasks = {
//    "Forest hunters",
//				"Befriend the pigs",        TIER2
//				"For a nice walk",          TIER2
//				"Kill the spiders",         SPIDER,TIER3
//				"Killer bees!",             TIER3
//				"Make a Beehat",            SPIDER, TIER1
//				"The hunters",              TIER4
//				"Magic meadow",             TIER4
//				"Hounded Greater Plains",   TIER4
//				"Merms ahoy",               SPIDER, TIER3
//				"Frogs and bugs",           TIER1
//		},

/// </summary>
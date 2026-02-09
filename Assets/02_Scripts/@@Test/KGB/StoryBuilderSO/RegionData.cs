using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New RegionData", menuName = "ScriptableObjects/TestWorld/Region Data")]
public class RegionData : ScriptableObject
{
    [Header("Basic Info")]
    public string RegionName;      // 구역(바이옴) 이름 (예: "Pig King Forest")
    public RegionBranch RoomBranch = RegionBranch.Default;     // room들의 배치를 결정하는 값 (예: 0.0 ~ 1.0 사이 값, region의 모양)
    public int DefaultMinCount;   // 이 구역에 방(Node)을 몇 개 만들지
    public int DefaultMaxCount;

    [Header("Room Configuration")]
    public RoomData EntranceRoom = null;  // 입구 방 (안전함)


    public List<EssentialRoomEntry> EssentialRooms = null;   // 필수 방 
    

    [Header("Default Room Pool")]
    [Tooltip("일반 노드에 랜덤하게 배치될 방 후보들")]
    public List<RoomData> DefaultRooms;

    [Header("Lock & Key System")]
    [Tooltip("이 구역에 들어가기 위해 필요한 열쇠들 (Locks)")]
    public List<string> LockIDs;

    [Tooltip("이 구역을 클리어하면 얻는 열쇠들 (Keys Given)")]
    // 예: "Gold_Key", "Magic_Staff"
    public List<string> GivenKeyIDs;

    public bool IsUnlockable(List<string> preRegionKeys)
    {
        if (LockIDs == null || LockIDs.Count == 0) return true;

        foreach (var lockID in LockIDs)
        {
            if (!LockResolver.CanUnlock(lockID, preRegionKeys))
                return false;                                   
        }
        return true;
    }

    public RoomData GetRandomDefaultRoom()
    {
        if (DefaultRooms == null || DefaultRooms.Count == 0) return null;
        return DefaultRooms[UnityEngine.Random.Range(0, DefaultRooms.Count)] ;
    }
}

[System.Serializable]
public class EssentialRoomEntry
{
    public RoomData RoomData;
    public RoomDepth Depth;
}

#region Enums
public enum RoomDepth
{
    Early,
    Mid,
    Late,
}

public enum RegionBranch
{
    Least,
    Default,
    Most,
}
#endregion
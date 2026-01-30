using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New RegionData", menuName = "ScriptableObjects/TestWorld/Region Data")]
public class RegionData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string regionName;      // 구역(바이옴) 이름 (예: "Pig King Forest")
    public string RegionName => regionName;

    [SerializeField] private float roomBranch;     // room들의 배치를 결정하는 값 (예: 0.0 ~ 1.0 사이 값, region의 모양)
    public float RoomBranch => roomBranch;
    [SerializeField] private int roomCount;   // 이 구역에 방(Node)을 몇 개 만들지
    public int RoomCount => roomCount;

    [Header("Room Configuration")]
    [SerializeField] private RoomData entranceRoom = null;  // 입구 방 (안전함)
    public RoomData EntranceRoom => entranceRoom;
    [SerializeField] private RoomData endRoom = null;   // 끝 방 
    public RoomData EndRoom => endRoom;

    [Header("Default Room Pool")]
    [Tooltip("일반 노드에 랜덤하게 배치될 방 후보들")]
    [SerializeField] private List<RoomData> defaultRooms;
    public List<RoomData> DefaultRooms => defaultRooms;

    [Header("Lock & Key System")]
    [Tooltip("이 구역에 들어가기 위해 필요한 열쇠들 (Locks)")]
    // 예: "Bridge_Repair" (다리를 고쳐야 건너감), "Fire_Resistance" (화염 저항 필요)
    [SerializeField] private List<LockData> locks = null;
    public List<LockData> Locks => locks;

    [Tooltip("이 구역을 클리어하면 얻는 열쇠들 (Keys Given)")]
    // 예: "Gold_Key", "Magic_Staff"
    [SerializeField] private List<KeyData> givenKeys;
    public List<KeyData> GivenKeys => givenKeys;

    public bool IsUnlockable(List<KeyData> preTaskKeys)
    {
        if (locks == null || locks.Count == 0) return true;

        foreach (var lockData in locks)
        {
            if (lockData == null) continue;

            // 하나라도 못 푸는 Lock이 있으면 -> 진입 불가!
            if (!lockData.CanUnlock(preTaskKeys))
                return false;
        }

        // 모든 Lock을 다 통과함 -> 진입 성공!
        return true;
    }




}

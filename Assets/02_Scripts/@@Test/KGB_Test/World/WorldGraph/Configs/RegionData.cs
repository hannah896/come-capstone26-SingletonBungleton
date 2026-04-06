using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[CreateAssetMenu(fileName = "New RegionData", menuName = "ScriptableObjects/TestWorld/Region Data")]
public class RegionData : ScriptableObject
{
    [Header("Basic Info")]
    public string RegionName;      // 구역(바이옴) 이름 (예: "Pig King Forest")
    
    [Tooltip("이 구역이 맵에서 차지하는 물리적 영토의 크기 배율 (1.0 = 표준, 2.0 = 두 배 넓음)")]
    [Range(0.1f, 10f)]
    public float TerritoryScale = 1.0f;
    [Tooltip("이 방의 밀도 (1.0 = 100% 꽉 채움, 0.1 = 10%만 채움)")]
    public float Density = 0.6f; // 밀도 (해당 지역의 어느정도의 밀도로 오브젝트를 배치할지)

    [Header("1. Count - BasedObjectData (개수 기반 배치)")]
    // 예: 보스, 웜홀, 금광맥 (개수 보장)
    [SerializeField] public List<PlacementRule> PlacementRules;
    [Header("3. POI (Point of Interest) - 특별 이벤트 지점 후보들")]
    [SerializeField] public List<POIData> POICandidates; // 특별 이벤트 후보들


    [Header("Height Settings")]
    [Tooltip("이 지역이 어떤 높이에서 융기되는지 설정하세요. (예: Plains, Highlands)")]
    public HeightLevel BaseHeightLevel = HeightLevel.Plains;

    [Tooltip("이 지역의 지형 형태(적용될 생성 전략)를 선택하세요.")]
    public LandformType LandformType = LandformType.Default;

    public BiomeData BiomeData; // 이 지역의 바이옴 데이터 (예: Forest, Desert)s



    [Tooltip("지형 생성 노이즈 파라미터 (직접 조정)")]
    public NoiseParameters TerrainNoiseParameters;

    

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


    //TODO: POI 어떻게 할당할지 고민해보기 (랜덤으로 뽑을지, 아니면 특정 조건에 맞는 POI를 배치할지)
    //public POIData GetRandomPOI(System.Random prng)
    //{

    //}

}

public class PlacementRule
{
    public string ruleName; // "HighGroundRocks", "RiversideTrees" 등
    public string prefabKey; // 실제 배치할 프리팹의 키값 (예: "Boss_OrcKing", "Decoration_Rock")

    [Header("Quantity Logic")]
    public bool isWeighted; // true면 가중치 기반, false면 개수 기반
    public float weight;    // 가중치 (isWeighted == true 일 때 사용)
    public int fixedCount;  // 확정 개수 (isWeighted == false 일 때 사용)
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
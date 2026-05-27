using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlacementRuleSet", menuName = "Scriptable Objects/TestWorld/PlacementRuleSet")]
public class PlacementRuleSet : ScriptableObject
{
    [SerializeField]
    public List<ObjectRule> ObjectRules;
    [SerializeField]
    public List<ItemRule> ItemRules;
}

[System.Serializable]
public struct SpawnShapeDensity
{
    public SpawnShape shape;
    [Range(0f, 1f)] public float density;
}

[System.Serializable]
public struct SpawnShapeCount
{
    public SpawnShape shape;
    [Min(0)] public int maxCount;
}

/// <summary>
/// Object - ResourceNode, Item, POI 등, 전반적으로 환경에 배치되는 오브젝트에 대한 룰
/// </summary>
[System.Serializable]
public class ObjectRule
{
    [Header("Rule Name")]
    public string ruleName;
    [Header("Prefab Key")]
    public string prefabKey;

    [Header("Quantity Logic")]
    public bool usePiece;       // 낱개 배치 
    public List<SpawnShapeDensity> pieceShapes;   // 낱개 배치 시 형태와 최대 밀도

    [Header("Cluster Logic")]
    public bool useCluster;     // 군집 배치 허용    
    public List<SpawnShapeDensity> clusterShapes;     // 군집 배치 시 군집 형태와 최대 밀도
    [Range(0f, 1f)] public float clusterDensity = 0.4f; // 반경 내에서의 배치 밀도
    [Min(0f)] public float clusterRadius = 4f;

    [Header("Spawn Pattern")]
    [Min(0.1f)] public float patternLength = 6f;
    [Range(-180f, 180f)] public float curveAngle = 30f;
}

[System.Serializable]
public class ItemRule
{
    [Header("Rule Name")]
    public string ruleName;
    [Header("Prefab Key")]
    public string prefabKey;

    [Header("Quantity Logic")]
    public bool usePiece;       // 낱개 배치 
    public List<SpawnShapeCount> pieceShapes;   // 낱개 배치 시 형태와 최대 개수

    [Header("Cluster Logic")]
    public bool useCluster;     // 군집 배치 
    public List<SpawnShapeCount> clusterShapes;     // 군집 배치 시 군집 형태와 군집의 최대 개수(1~최대 개수 사이)
    [Min(1)] public int itemMinCount = 2;
    [Min(1)] public int itemMaxCount = 4;               // 군집 내 개수 범위
    [Min(0f)] public float clusterRadius = 3f;

    [Header("Spawn Pattern")]
    [Min(0.1f)] public float patternLength = 6f;
    [Range(-180f, 180f)] public float curveAngle = 30f;
}

public enum SpawnShape
{
    Default = 0,            // 단일 오브젝트 배치
    Line = 2,               // 일자 형태 배치
    Curve = 3,              // 곡선 형태 배치
    Branching = 4           // 갈라지는 형태 배치
}
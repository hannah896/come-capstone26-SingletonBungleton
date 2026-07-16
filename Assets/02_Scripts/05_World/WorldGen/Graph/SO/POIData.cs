using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New POI Data", menuName = "Scriptable Objects/TestWorld/POI Data")]
public abstract class POIData : ScriptableObject
{
    [SerializeField] public string POIName;

    [Header("1. Count - Based ObjectData Objects (필수 배치)")]
    // 예: 보스, 웜홀, 금광맥 (개수 보장)
    [SerializeField] public List<DisposeRuleSet> DisposeRules;
    [SerializeField] public string TopKey;    // 예: "Forest_Top", "Desert_Top"
}

public class RandomPOIData : POIData
{
    [SerializeField] public float SpawnChance;
}


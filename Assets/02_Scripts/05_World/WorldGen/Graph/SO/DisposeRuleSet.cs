using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DisposeRuleSet", menuName = "Scriptable Objects/TestWorld/DisposeRuleSet")]
public class DisposeRuleSet : ScriptableObject
{
    [Header("---밀도 기반---")]
    [SerializeField]
    public List<DensityDisposeRule> ObjectRules;

    [Header("---개수 기반---")]
    [SerializeField]
    public List<CountDisposeRule> ItemRules;


    [SerializeField]
    public List<CountDisposeRule> SpawnerRules;
}


// -------------------------------------------------------------
// 1. 밀도 기반 배치 규칙 (밀도 기반: 나무, 바위, 수풀 등)
// -------------------------------------------------------------
[System.Serializable]
public class DensityDisposeRule
{
    public string ruleName;
    public string prefabKey;

    [Header("Spawn Density")]
    [Range(0f, 1f)] public float density;

}


// -------------------------------------------------------------
// 2. 개수 기반 배치 규칙 (개수 기반: 부싯돌, 나뭇가지, 특수 상자 등)
// -------------------------------------------------------------
[System.Serializable]
public class CountDisposeRule
{
    public string ruleName;
    public string prefabKey;

    [Header("Spawn Quantity")]
    [Min(0)] public int minCount; // 최소 보장 개수
    [Min(0)] public int maxCount; // 최대 스폰 개수

}






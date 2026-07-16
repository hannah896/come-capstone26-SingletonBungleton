using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlacementRuleSet", menuName = "Scriptable Objects/TestWorld/PlacementRuleSet")]
public class DisposeRuleSet : ScriptableObject
{
    [SerializeField]
    public List<ObjectDisposeRule> ObjectRules;
    [SerializeField]
    public List<ItemDisposeRule> ItemRules;
}

// -------------------------------------------------------------
// 1. 아이템 규칙 (개수 기반: 부싯돌, 나뭇가지, 특수 상자 등)
// -------------------------------------------------------------
[System.Serializable]
public class ItemDisposeRule
{
    public string ruleName;
    public string prefabKey;

    [Header("Spawn Quantity")]
    [Min(0)] public int minCount; // 최소 보장 개수
    [Min(0)] public int maxCount; // 최대 스폰 개수

}

// -------------------------------------------------------------
// 2. 환경 오브젝트 규칙 (밀도 기반: 나무, 바위, 수풀 등)
// -------------------------------------------------------------
[System.Serializable]
public class ObjectDisposeRule
{
    public string ruleName;
    public string prefabKey;

    [Header("Spawn Density")]
    [Range(0f, 1f)] public float density;

}





using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlacementRuleSet", menuName = "Scriptable Objects/TestWorld/PlacementRuleSet")]
public class PlacementRuleSet : ScriptableObject
{
    [SerializeField]
    public List<PlacementRule> Rules;
}

[System.Serializable]
public class PlacementRule
{
    public string prefabKey; // 실제 배치할 프리팹의 키값 (예: "Boss_OrcKing", "Decoration_Rock")

    [Header("Quantity Logic")]
    public bool isWeighted; // true면 가중치 기반, false면 개수 기반
    public float weight;    // 가중치 (isWeighted == true 일 때 사용)
    public int fixedCount;  // 확정 개수 (isWeighted == false 일 때 사용)
}

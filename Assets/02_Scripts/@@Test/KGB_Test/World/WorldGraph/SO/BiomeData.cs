using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Biome Data", menuName = "Scriptable Objects/TestWorld/Biome Data")]
public class BiomeData : ScriptableObject
{
    [Tooltip("이 지역의 바이옴 유형을 선택하세요. (예: Forest, Desert)")]
    [SerializeField] public BiomeType BiomeType;
    [SerializeField] public Color DebugColor = Color.green; // 기즈모용 색상

    [Header("Assets")]
    // GameObject 직접 참조 대신 키값(string) 사용
    [SerializeField] public string TopKey;    // 예: "Forest_Top", "Desert_Top"
    [SerializeField] public string AltKey;    // 예: "Forest_Top", "Desert_Top"
    [SerializeField] public string CliffKey;  // 예: "Highlands_Cliff", "Forest_Cliff"

    // 터레인 위에 배치되는 디테일 오브젝트들 (예: 풀, 꽃)
    [SerializeField] public List<string> DetailKeys;    // 예: "Detail_Grass", "Detail_Flower"
    [SerializeField] public List<string> DecoKeys;      // 예: "Deco_Rock", "Deco_Tree"

}
public enum BiomeType
{
    Plains,
    Forest,
    Desert,
    Snow,
    Swamp,
    Savanna,
    Rocky,
    Highlands,
}


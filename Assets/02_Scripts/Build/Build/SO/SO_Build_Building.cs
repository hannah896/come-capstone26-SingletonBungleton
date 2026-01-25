using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "ScriptableObjects/Build/Building")]
public class SO_Build_Building : ScriptableObject
{
    [Header("기본 정보")]
    public string Id;
    public string Name;
    public GameObject Prefab;

    [Header("UI 정보")]
    public Sprite Icon;
    [TextArea] public string Description;

    [Header("아이소메트릭 모양 설정")]
    public List<Vector2Int> Footprint = new List<Vector2Int> { new Vector2Int(0, 0) };
}

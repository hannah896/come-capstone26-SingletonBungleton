using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "Test_Map/Biome Data")]
public class BiomeData : ScriptableObject
{
    public string id;
    public Color debugColor = Color.green; // 기즈모용 색상

    [Header("Assets")]
    public GameObject groundPrefab;                 // 바닥 타일
    public List<SpawnableObject> spawnableObjects;  // 나무, 돌, 몬스터(자연 스폰 가능한 오브젝트 목록)
}

[System.Serializable]
public class SpawnableObject
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance = 0.1f; // 등장 확률
}
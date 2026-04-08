using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New BiomeData", menuName = "ScriptableObjects/TestMap/Biome Data")]
public class BiomeData : ScriptableObject
{
    public string id;
    public int tier = 1;                    // 바이옴 등급
    public Color debugColor = Color.green; // 기즈모용 색상

    [Header("Assets")]
    public GameObject groundPrefab;                         // 바닥 타일
    [Header("1. Essential Objects (필수 배치)")]
    // 예: 보스, 웜홀, 금광맥 (개수 보장)
    public List<FixedSpawnalbeObjectData> essentialObjects;
    [Header("2. 퍼센트/밀도 배치 (Density)")]
    // 예: 나무, 풀 (땅 크기에 비례해서 빽빽하게)
    public List<SpawnableData> densityObjects;
    
}

[System.Serializable]
public class SpawnableData
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance = 0.1f; // 등장 확률
}

public class FixedSpawnalbeObjectData
{
    public GameObject prefab;
    public int minCount = 1; // 최소 1개는 무조건 나온다!
    public int maxCount = 1;
}
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Room Data", menuName = "ScriptableObjects/TestWorld/Room Data")]
public class RoomData : ScriptableObject
{
    [SerializeField] public string RoomName;
    [Tooltip("이 방의 밀도 (1.0 = 100% 꽉 채움, 0.1 = 10%만 채움)")]
    public float Density; // 밀도 (해당 룸의 어느정도의 밀도로 오브젝트를 배치할지)
    public Color DebugColor = Color.green; // 기즈모용 색상

    [Header("Assets")]
    [SerializeField] public string GroundPrefab;  
    [Header("1. Essential Objects (필수 배치)")]
    // 예: 보스, 웜홀, 금광맥 (개수 보장)
    [SerializeField] public List<EssentialObjectData> EssentialObjects;
    [Header("2. Weighted Objects 가중치 기반 배치(에센셜에도 넣어줘야함)")]
    // 예: 나무, 풀 (가중치 랜덤 알고리즘으로 배치)
    [SerializeField] public List<WeightedObjectData> WeightedObjects;


   

}

[System.Serializable]
public class WeightedObjectData 
{
    public string PrefabName;
    [Range(0f, 1f)] public float Weight = 1.0f; // 가중치(합산되어 비율로 변환됨)
}

[System.Serializable]
public class EssentialObjectData 
{
    public string PrefabName;
    public int MinCount = 1; // 최소 1개는 무조건 나온다!
    public int MaxCount = 1;
}
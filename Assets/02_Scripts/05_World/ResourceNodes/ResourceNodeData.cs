using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewResourceNodeData", menuName = "Scriptable Objects/ResourceNodeData")] 
public class ResourceNodeData : ScriptableObject
{
    [Header("Stats")]
    public int MaxHealth = 3;               // 자원 노드의 최대 체력
    public int GatherAmount = 1;            // 채집 용 횟수
    public int RespawnTime = 300;           // 자원 노드가 파괴된 후 재생성되기까지의 시간 (초 단위)
    public string[] AllowedToolIds;

    [Header("Drops")]
    public DropItemData[] Drops;
}


[Serializable]
public class DropItemData
{
    public string DropPrefabKey;
    public int MinDropCount = 1;
    public int MaxDropCount = 1;

    // 확률 시스템 추가: 0.0 ~ 1.0 (1.0이면 100% 드롭)
    [Range(0f, 1f)]
    public float DropChance = 1.0f;
}
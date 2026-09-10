using System;
using UnityEngine;

/// <summary>
/// 실제 게임에서 배치된 자원 노드가 가지는 데이터를 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewResourceNodeData", menuName = "Scriptable Objects/ResourceNodeData")] 
public class ResourceNodeData : ScriptableObject
{
    [Header("Meta")]
    public string Name;

    [Header("Type")]
    public ResourceNodeType ResourceNodeType;

    [Header("Stats")]
    public int MaxHealth = 3;               // 자원 노드의 최대 체력
    public int GatherAmount = 1;            // 채집 용 횟수
    public int RespawnTime = 300;           // 자원 노드가 파괴된 후 재생성되기까지의 시간 (초 단위)

    [Header("Required Harvest Tool")]
    [Tooltip("이 자원을 채집하기 위해 필요한 도구 (None = 맨손 가능)")]
    public HarvestToolType RequiredTool = HarvestToolType.None;

    [Header("Drops")]
    public DropData[] Drops;

    public float DropRadius = 0.5f;
    public string DropFxPrefabKey;

    [Tooltip("체크하면 드롭 아이템을 땅에 흩뿌리지 않고 채집한 플레이어의 인벤토리에 바로 넣는다.")]
    public bool GatherDirectlyToInventory = false;

    [Tooltip("체크하면 도구 없이 G키(줍기)로 바로 채집된다. (풀 등)")]
    public bool HandPickable = false;
    private void OnValidate()
    {
        Name = name;
    }
}

public enum ResourceNodeType
{
    None = 0,
    Tree = 1,
    Mine = 2,
    Bush = 3,
}

[Serializable]
public class DropData
{
    public string DropPrefabKey;
    public int MinDropCount = 1;
    public int MaxDropCount = 1;



    // 확률 시스템 추가: 0.0 ~ 1.0 (1.0이면 100% 드롭)
    [Range(0f, 1f)]
    public float DropChance = 1.0f;
}
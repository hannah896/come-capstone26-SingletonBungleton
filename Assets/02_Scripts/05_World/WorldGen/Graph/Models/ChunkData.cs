using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 청크 1개의 데이터를 담는 클래스 (예: 64x64 타일)
/// </summary>
public class ChunkData
{
    public Vector2Int ChunkCoord { get; private set; }

    // 청크 내부의 로컬 데이터 (크기: ChunkSize x ChunkSize)
    public int[,] TerritoryMap { get; set; }
    public float[,] HeightMap { get; set; }
    public float[,] NoiseMap { get; set; }

    // 오브젝트 배치 데이터 (해당 청크에 속한 배치물들)
    public List<PlacementData> PlacementDatas { get; set; } = new();

    // 청크 내에서 파괴된 오브젝트들의 인스턴스 ID를 저장하는 집합(아이디, 파괴된 시간)
    public Dictionary<int, float> DestroyedObjects = new Dictionary<int, float>();
    public ChunkData(Vector2Int coord, int chunkSize)
    {
        ChunkCoord = coord;
        TerritoryMap = new int[chunkSize + 1, chunkSize + 1];
        HeightMap = new float[chunkSize + 1, chunkSize + 1];
        NoiseMap = new float[chunkSize + 1, chunkSize + 1];
    }

    public bool IsObjectDestroyed(int instanceId)
    {
        if (instanceId == 0) return false;
        return DestroyedObjects.ContainsKey(instanceId);
    }

    public void MarkObjectDestroyed(int instanceId, float targetRespawnTime)
    {
        if (!DestroyedObjects.ContainsKey(instanceId))
        {
            DestroyedObjects.Add(instanceId, targetRespawnTime);
        }
    }
}
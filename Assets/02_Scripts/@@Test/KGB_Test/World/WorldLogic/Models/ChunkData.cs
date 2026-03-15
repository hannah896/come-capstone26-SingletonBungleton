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
    public List<DisposeData> DisposedObjects { get; set; } = new();

    public ChunkData(Vector2Int coord, int chunkSize)
    {
        ChunkCoord = coord;
        TerritoryMap = new int[chunkSize, chunkSize];
        HeightMap = new float[chunkSize, chunkSize];
        NoiseMap = new float[chunkSize, chunkSize];
    }
}
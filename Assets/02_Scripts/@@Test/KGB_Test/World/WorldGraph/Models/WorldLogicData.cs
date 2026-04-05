using System.Collections.Concurrent;
using UnityEngine;


[System.Serializable]
public struct InfluenceData
{
    public float Distance;
    public float Weight;
}

/// <summary>
/// 맵의 2D/3D 배열 데이터를 담고 있는 컨테이너 클래스
/// </summary>
public class WorldLogicData
{
    public int ChunkSize { get; private set; } // 1개 청크의 가로세로 크기
    public Vector2Int TerrainSize { get; private set; }

    private ConcurrentDictionary<Vector2Int, ChunkData> _chunks = new();

    public int[,] TerritoryWorld { get; set; }  // -1: 바다/벽, >=0: 노드 인덱스
    public float[,] HeightWorld { get; set; }   // 타일별 실제 높이
    public int[,] BorderWorld { get; set; }     // -2: 경계선
    public float[,] NoiseWorld { get; set; }    // 펄린 노이즈 캐시

    public int[,] DistanceToOceanWorld { get; set; } // 해안선으로부터의 타일 칸 수
    public InfluenceData[,] CoastlineDataWorld { get; set; }     // 해안선 거리 기반 영향력 (0.4 ~ 1.2)
    public InfluenceData[,] RegionEdgeDataWorld { get; set; } // 지역 경계 거리 기반 영향력 (0.0 ~ 1.0)

    public WorldLogicData(Vector2Int gridSize, int chunkSize)
    {
        TerrainSize = gridSize;
        ChunkSize = chunkSize;

        //TODO: 거대한 배열 생성은 메모리 문제를 일으킬 수 있으므로, 필요할 때마다 청크 단위로 생성하는 방식으로 변경할 예정
        TerritoryWorld = new int[gridSize.x, gridSize.y];
        HeightWorld = new float[gridSize.x, gridSize.y];
        BorderWorld = new int[gridSize.x, gridSize.y];
        NoiseWorld = new float[gridSize.x, gridSize.y];
        DistanceToOceanWorld = new int[gridSize.x, gridSize.y];

        CoastlineDataWorld = new InfluenceData[gridSize.x, gridSize.y];
        RegionEdgeDataWorld = new InfluenceData[gridSize.x, gridSize.y];
    }



    /// <summary>
    /// 특정 청크 좌표의 데이터를 가져오거나 새로 생성합니다.
    /// </summary>
    public ChunkData GetOrCreateChunk(Vector2Int chunkCoord)
    {
        return _chunks.GetOrAdd(chunkCoord, coord => new ChunkData(coord, ChunkSize));
    }
    /// <summary>
    /// 월드 타일 좌표를 기반으로 해당 위치의 청크 좌표를 계산합니다.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public Vector2Int GetChunkCoord(int tileX, int tileY)
    {
        return new Vector2Int(Mathf.FloorToInt((float)tileX / ChunkSize), Mathf.FloorToInt((float)tileY / ChunkSize));
    }

    public float GetHeightAt(int x, int y)
    {
        // 
        Vector2Int chunkCoord = GetChunkCoord(x, y);
        if (_chunks.TryGetValue(chunkCoord, out var chunk))
        {
            // 월드 좌표를 청크 내부 로컬 좌표로 변환
            int localX = x % ChunkSize;
            int localY = y % ChunkSize;
            // 음수 좌표 보정
            if (localX < 0) localX += ChunkSize;
            if (localY < 0) localY += ChunkSize;

            return chunk.HeightMap[localX, localY];
        }
        return 0f; // 청크가 로드되지 않은 바다/빈 공간
    }


    public int GetRegionAt(int x, int y)
    {
        if (x < 0 || x >= TerrainSize.x || y < 0 || y >= TerrainSize.y)
            return -1; // OCEAN_MARKER
        return TerritoryWorld[x, y];
    }
    
}
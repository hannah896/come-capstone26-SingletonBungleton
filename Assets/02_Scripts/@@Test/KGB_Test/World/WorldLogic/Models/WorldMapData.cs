using UnityEngine;

/// <summary>
/// 맵의 2D/3D 배열 데이터를 담고 있는 컨테이너 클래스
/// </summary>
public class WorldLogicData
{
    public Vector2Int TileGridSize { get; private set; }

    public int[,] TerritoryWorld { get; set; }  // -1: 바다/벽, >=0: 노드 인덱스
    public float[,] HeightWorld { get; set; }   // 타일별 실제 높이
    public int[,] BorderWorld { get; set; }     // -2: 경계선
    public float[,] NoiseWorld { get; set; }    // 펄린 노이즈 캐시

    public WorldLogicData(Vector2Int gridSize)
    {
        TileGridSize = gridSize;
        TerritoryWorld = new int[gridSize.x, gridSize.y];
        HeightWorld = new float[gridSize.x, gridSize.y];
        BorderWorld = new int[gridSize.x, gridSize.y];
        NoiseWorld = new float[gridSize.x, gridSize.y];
    }

    public int GetRegionAt(int x, int y)
    {
        if (x < 0 || x >= TileGridSize.x || y < 0 || y >= TileGridSize.y)
            return -1; // OCEAN_MARKER
        return TerritoryWorld[x, y];
    }

    public float GetHeightAt(int x, int y)
    {
        if (HeightWorld == null) return 0f;
        if (x < 0 || x >= TileGridSize.x || y < 0 || y >= TileGridSize.y)
            return 0f;
        return HeightWorld[x, y];
    }
}
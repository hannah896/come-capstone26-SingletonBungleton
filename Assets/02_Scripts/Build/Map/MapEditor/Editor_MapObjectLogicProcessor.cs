using System.Collections.Generic;
using UnityEngine;

public static class Editor_MapObjectLogicProcessor
{
    public static MapObjectType[,] Apply2x2Constraint(MapObjectType[,] grid, int w, int h)
    {
        MapObjectType[,] result = (MapObjectType[,])grid.Clone();
        for (int y = 0; y < h - 1; y++)
        {
            for (int x = 0; x < w - 1; x++)
            {
                if (grid[x, y] != MapObjectType.None)
                {
                    MapObjectType type = grid[x, y];
                    result[x, y] = type;
                    result[x + 1, y] = type;
                    result[x, y + 1] = type;
                    result[x + 1, y + 1] = type;
                }
            }
        }
        return result;
    }

    public static void GetMassCells(int x, int y, MapObjectType type, MapObjectType[,] grid, bool[,] visited, List<Vector2Int> result)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        if (visited[x, y] || grid[x, y] != type) return;

        visited[x, y] = true;
        result.Add(new Vector2Int(x, y));

        GetMassCells(x + 1, y, type, grid, visited, result);
        GetMassCells(x - 1, y, type, grid, visited, result);
        GetMassCells(x, y + 1, type, grid, visited, result);
        GetMassCells(x, y - 1, type, grid, visited, result);
    }
}

using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Generates Poisson-disk samples from a region's discrete tile set.
/// </summary>
public static class PointSampler
{
    public static async UniTask<List<Vector2Int>> GeneratePoissonPointsAsync(
        IReadOnlyList<Vector2Int> availableTiles,
        int maxPointCount,
        float minimumDistance,
        int maxSamplingAttempts,
        System.Random random,
        CancellationToken ct)
    {
        if (availableTiles == null || availableTiles.Count == 0 || maxPointCount <= 0)
            return new List<Vector2Int>();

        if (random == null)
            throw new System.ArgumentNullException(nameof(random));

        int targetCount = Mathf.Min(maxPointCount, availableTiles.Count);
        if (minimumDistance <= 0f)
            return TakeRandomUniqueTiles(availableTiles, targetCount, random, ct);

        float cellSize = minimumDistance / Mathf.Sqrt(2f);
        int minX = availableTiles[0].x;
        int maxX = minX;
        int minY = availableTiles[0].y;
        int maxY = minY;

        for (int i = 1; i < availableTiles.Count; i++)
        {
            Vector2Int tile = availableTiles[i];
            minX = Mathf.Min(minX, tile.x);
            maxX = Mathf.Max(maxX, tile.x);
            minY = Mathf.Min(minY, tile.y);
            maxY = Mathf.Max(maxY, tile.y);
        }

        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>(availableTiles);
        int gridWidth = Mathf.CeilToInt((maxX - minX + 1) / cellSize);
        int gridHeight = Mathf.CeilToInt((maxY - minY + 1) / cellSize);
        Vector2Int?[,] grid = new Vector2Int?[gridWidth + 1, gridHeight + 1];
        List<Vector2Int> activeList = new List<Vector2Int>();
        List<Vector2Int> result = new List<Vector2Int>(targetCount);

        Vector2Int startPoint = availableTiles[random.Next(0, availableTiles.Count)];
        InsertPoint(startPoint, grid, activeList, result, minX, minY, cellSize);

        float sqrMinDistance = minimumDistance * minimumDistance;
        int attemptsPerPoint = Mathf.Max(1, maxSamplingAttempts);
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (activeList.Count > 0 && result.Count < targetCount)
        {
            ct.ThrowIfCancellationRequested();

            int randomIndex = random.Next(0, activeList.Count);
            Vector2Int currentPoint = activeList[randomIndex];
            bool foundValid = false;

            for (int attempt = 0; attempt < attemptsPerPoint; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = minimumDistance + ((float)random.NextDouble() * minimumDistance);
                Vector2Int candidate = new Vector2Int(
                    currentPoint.x + Mathf.RoundToInt(Mathf.Cos(angle) * distance),
                    currentPoint.y + Mathf.RoundToInt(Mathf.Sin(angle) * distance));

                if (tileSet.Contains(candidate) && IsValidPoissonPoint(
                    candidate, grid, sqrMinDistance, minX, minY, cellSize, gridWidth, gridHeight))
                {
                    InsertPoint(candidate, grid, activeList, result, minX, minY, cellSize);
                    foundValid = true;
                    break;
                }
            }

            if (!foundValid)
            {
                activeList[randomIndex] = activeList[activeList.Count - 1];
                activeList.RemoveAt(activeList.Count - 1);
            }

            if (stopwatch.ElapsedMilliseconds > 10)
            {
                await UniTask.Yield(ct);
                stopwatch.Restart();
            }
        }

        return result;
    }

    private static List<Vector2Int> TakeRandomUniqueTiles(
        IReadOnlyList<Vector2Int> availableTiles,
        int targetCount,
        System.Random random,
        CancellationToken ct)
    {
        List<Vector2Int> candidates = new List<Vector2Int>(availableTiles);
        List<Vector2Int> result = new List<Vector2Int>(targetCount);

        while (result.Count < targetCount && candidates.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            int index = random.Next(0, candidates.Count);
            result.Add(candidates[index]);
            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);
        }

        return result;
    }

    private static void InsertPoint(
        Vector2Int point,
        Vector2Int?[,] grid,
        List<Vector2Int> activeList,
        List<Vector2Int> result,
        int minX,
        int minY,
        float cellSize)
    {
        int gridX = Mathf.FloorToInt((point.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((point.y - minY) / cellSize);

        if (gridX >= 0 && gridX < grid.GetLength(0) && gridY >= 0 && gridY < grid.GetLength(1))
        {
            grid[gridX, gridY] = point;
            activeList.Add(point);
            result.Add(point);
        }
    }

    private static bool IsValidPoissonPoint(
        Vector2Int candidate,
        Vector2Int?[,] grid,
        float sqrMinDistance,
        int minX,
        int minY,
        float cellSize,
        int gridWidth,
        int gridHeight)
    {
        int gridX = Mathf.FloorToInt((candidate.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((candidate.y - minY) / cellSize);

        const int SearchRadius = 2;
        for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
        {
            for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
            {
                int neighborX = gridX + dx;
                int neighborY = gridY + dy;
                if (neighborX < 0 || neighborX > gridWidth || neighborY < 0 || neighborY > gridHeight)
                    continue;

                Vector2Int? neighbor = grid[neighborX, neighborY];
                if (!neighbor.HasValue)
                    continue;

                float distanceX = candidate.x - neighbor.Value.x;
                float distanceY = candidate.y - neighbor.Value.y;
                if ((distanceX * distanceX) + (distanceY * distanceY) < sqrMinDistance)
                    return false;
            }
        }

        return true;
    }
}

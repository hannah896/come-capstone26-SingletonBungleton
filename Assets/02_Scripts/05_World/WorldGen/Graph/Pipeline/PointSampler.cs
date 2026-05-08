using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class PointSampler : IGraphPipelineStage
{
    private WorldSettings _worldSettings;
    private DisposeSettings _disposeSettings;
    private System.Random _prng;
    private System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

    private CancellationToken _ct;
    private List<Node> _nodes;

    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _disposeSettings = _worldSettings.DisposeSettings;
        _prng = new System.Random(_worldSettings.WorldSeed + (int)WorldSeedChannel.ObjectDisposer);
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _ct = ct;
        _nodes = ctx.GraphData?.Nodes;

        _stopwatch.Restart();

        if (_nodes == null) return;

        foreach (Node node in _nodes)
        {
            _ct.ThrowIfCancellationRequested();

            if (!IsValidNode(node))
                continue;

            List<Vector2Int> points = await GeneratePoissonPointsAsync(node.OwnedTiles, node.RegionData.Density);
            // 노드에 생성된 후보 지점 기록
            node.CandidatePoints = points;

            if (_stopwatch.ElapsedMilliseconds > 10)
            {
                await UniTask.Yield(_ct);
                _stopwatch.Restart();
            }
        }
    }

    private bool IsValidNode(Node node)
    {
        if (node == null || node.RegionData == null)
            return false;

        if (node.OwnedTiles == null || node.OwnedTiles.Count == 0)
            return false;

        if (node.RegionData.Density <= 0f)
            return false;

        return true;
    }

    private async UniTask<List<Vector2Int>> GeneratePoissonPointsAsync(
        List<Vector2Int> availableTiles,
        float density)
    {
        if (availableTiles == null || availableTiles.Count == 0)
            return new List<Vector2Int>();

        float adjustedMinDistance = _disposeSettings.minObjectDistance / Mathf.Clamp(density, 0.1f, 2f);

        int minX = availableTiles.Min(t => t.x);
        int maxX = availableTiles.Max(t => t.x);
        int minY = availableTiles.Min(t => t.y);
        int maxY = availableTiles.Max(t => t.y);

        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>(availableTiles);

        float cellSize = adjustedMinDistance / Mathf.Sqrt(2);
        int gridWidth = Mathf.CeilToInt((maxX - minX + 1) / cellSize);
        int gridHeight = Mathf.CeilToInt((maxY - minY + 1) / cellSize);

        Vector2Int?[,] grid = new Vector2Int?[gridWidth + 1, gridHeight + 1];
        List<Vector2Int> activeList = new List<Vector2Int>();
        List<Vector2Int> result = new List<Vector2Int>();

        Vector2Int startPoint = availableTiles[_prng.Next(0, availableTiles.Count)];
        InsertPoint(startPoint, grid, activeList, result, minX, minY, cellSize);

        float sqrMinDistance = adjustedMinDistance * adjustedMinDistance;

        while (activeList.Count > 0)
        {
            _ct.ThrowIfCancellationRequested();

            int randomIndex = _prng.Next(0, activeList.Count);
            Vector2Int currentPoint = activeList[randomIndex];
            bool foundValid = false;

            for (int attempt = 0; attempt < _disposeSettings.maxSamplingAttempts; attempt++)
            {
                float angle = (float)_prng.NextDouble() * Mathf.PI * 2f;
                float distance = adjustedMinDistance + ((float)_prng.NextDouble() * adjustedMinDistance);

                int newX = currentPoint.x + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
                int newY = currentPoint.y + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
                Vector2Int candidate = new Vector2Int(newX, newY);

                if (tileSet.Contains(candidate) && IsValidPoissonPoint(candidate, grid, sqrMinDistance, minX, minY, cellSize, gridWidth, gridHeight))
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

            if (_stopwatch.ElapsedMilliseconds > 10)
            {
                await UniTask.Yield(_ct);
                _stopwatch.Restart();
            }
        }

        return result;
    }

    private void InsertPoint(Vector2Int point, Vector2Int?[,] grid, List<Vector2Int> activeList, List<Vector2Int> result, int minX, int minY, float cellSize)
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

    private bool IsValidPoissonPoint(Vector2Int candidate, Vector2Int?[,] grid, float sqrMinDistance, int minX, int minY, float cellSize, int gridWidth, int gridHeight)
    {
        int gridX = Mathf.FloorToInt((candidate.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((candidate.y - minY) / cellSize);

        int searchRadius = 2;
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                int nx = gridX + dx;
                int ny = gridY + dy;

                if (nx >= 0 && nx <= gridWidth && ny >= 0 && ny <= gridHeight)
                {
                    Vector2Int? neighbor = grid[nx, ny];
                    if (neighbor.HasValue)
                    {
                        float dxDist = candidate.x - neighbor.Value.x;
                        float dyDist = candidate.y - neighbor.Value.y;
                        float sqrDistance = (dxDist * dxDist) + (dyDist * dyDist);
                        if (sqrDistance < sqrMinDistance)
                            return false;
                    }
                }
            }
        }

        return true;
    }
}
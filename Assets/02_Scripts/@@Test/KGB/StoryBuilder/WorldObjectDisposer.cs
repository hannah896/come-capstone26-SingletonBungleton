using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// 5단계: 오브젝트 배치를 담당하는 클래스
/// 푸아송 디스크 샘플링과 밀도 기반 배치를 지원
/// </summary>
public class WorldObjectDisposer
{
    #region Settings
    [System.Serializable]
    public class SpawnSettings
    {
        [Header("Poisson Disk Sampling")]
        public float minObjectDistance = 2f;     // 오브젝트 간 최소 거리
        public int maxSamplingAttempts = 30;     // 푸아송 샘플링 시도 횟수
        
        [Header("Performance")]
        public int batchSize = 100;              // 비동기 처리 배치 크기
    }
    #endregion

    #region Spawn Result
    /// <summary>
    /// 스폰 결과 데이터
    /// </summary>
    public class SpawnResult
    {
        public string prefabName;
        public Vector3 worldPosition;
    }
    #endregion

    private SpawnSettings _settings;
    private List<SpawnResult> _spawnResults = new();

    public List<SpawnResult> SpawnResults => _spawnResults;

    public WorldObjectDisposer(SpawnSettings settings = null)
    {
        _settings = settings ?? new SpawnSettings();
    }

    /// <summary>
    /// 모든 영역에 오브젝트 배치
    /// </summary>
    public async UniTask SpawnObjectsAsync(
        List<Node> nodes,
        float globalDensityMultiplier,
        CancellationToken ct)
    {
        _spawnResults.Clear();

        if (nodes == null) return;

        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();
            
            if (node == null || node.RoomData == null) 
                continue;
            
            if (node.OwnedTiles == null || node.OwnedTiles.Count == 0)
                continue;
            
            // density가 0 이하면 배치하지 않음
            if (node.RoomData.density <= 0f)
                continue;

            await SpawnRegionObjectsAsync(node, globalDensityMultiplier, ct);
        }
    }

    /// <summary>
    /// 단일 영역에 오브젝트 배치
    /// </summary>
    private async UniTask SpawnRegionObjectsAsync(
        Node region,
        float globalDensityMultiplier,
        CancellationToken ct)
    {
        RoomData roomData = region.RoomData;
        float effectiveDensity = roomData.density * globalDensityMultiplier;
        
        // 푸아송 디스크 샘플링으로 배치 가능 위치 생성
        List<Vector2Int> validPositions = await GeneratePoissonPointsAsync(
            region.OwnedTiles,
            effectiveDensity,
            ct
        );

        if (validPositions.Count == 0) return;

        // 위치 셔플 (자연스러운 배치를 위해)
        ShuffleList(validPositions);

        int positionIndex = 0;

        // 1. Essential Objects 배치 (확정 개수)
        if (roomData.essentialObjects != null)
        {
            foreach (var essential in roomData.essentialObjects)
            {
                if (essential == null || string.IsNullOrEmpty(essential.prefabName))
                    continue;
                
                int count = Random.Range(essential.minCount, essential.maxCount + 1);
                
                for (int i = 0; i < count && positionIndex < validPositions.Count; i++)
                {
                    Vector2Int tile = validPositions[positionIndex++];
                    _spawnResults.Add(new SpawnResult
                    {
                        prefabName = essential.prefabName,
                        worldPosition = TileToWorldPosition(tile)
                    });
                }
            }
        }

        // 2. Proportion Objects 배치 (가중치 기반)
        if (roomData.proportionObjects != null && roomData.proportionObjects.Count > 0)
        {
            int remainingPositions = validPositions.Count - positionIndex;
            int proportionCount = Mathf.RoundToInt(remainingPositions * effectiveDensity);
            
            // 가중치 정규화
            float totalWeight = roomData.proportionObjects.Sum(p => p?.weight ?? 0f);
            if (totalWeight <= 0) return;

            for (int i = 0; i < proportionCount && positionIndex < validPositions.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                
                // 가중치 기반 랜덤 선택
                string selectedPrefab = SelectWeightedRandom(roomData.proportionObjects, totalWeight);
                
                if (!string.IsNullOrEmpty(selectedPrefab))
                {
                    Vector2Int tile = validPositions[positionIndex++];
                    _spawnResults.Add(new SpawnResult
                    {
                        prefabName = selectedPrefab,
                        worldPosition = TileToWorldPosition(tile),
                        regionId = region.id,
                        isEssential = false
                    });
                }

                if (i % _settings.batchSize == 0)
                {
                    await UniTask.Yield(ct);
                }
            }
        }
    }

    #region Poisson Disk Sampling
    /// <summary>
    /// 푸아송 디스크 샘플링으로 균등 분포 위치 생성
    /// </summary>
    private async UniTask<List<Vector2Int>> GeneratePoissonPointsAsync(
        List<Vector2Int> availableTiles,
        float density,
        CancellationToken ct)
    {
        if (availableTiles == null || availableTiles.Count == 0)
            return new List<Vector2Int>();

        // 밀도에 따른 최소 거리 조정
        float adjustedMinDistance = _settings.minObjectDistance / Mathf.Clamp(density, 0.1f, 2f);
        
        // 바운딩 박스 계산
        int minX = availableTiles.Min(t => t.x);
        int maxX = availableTiles.Max(t => t.x);
        int minY = availableTiles.Min(t => t.y);
        int maxY = availableTiles.Max(t => t.y);

        // 유효 타일 해시셋 (빠른 검색용)
        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>(availableTiles);

        // 그리드 기반 샘플링
        float cellSize = adjustedMinDistance / Mathf.Sqrt(2);
        int gridWidth = Mathf.CeilToInt((maxX - minX + 1) / cellSize);
        int gridHeight = Mathf.CeilToInt((maxY - minY + 1) / cellSize);
        
        Vector2Int?[,] grid = new Vector2Int?[gridWidth + 1, gridHeight + 1];
        List<Vector2Int> activeList = new List<Vector2Int>();
        List<Vector2Int> result = new List<Vector2Int>();

        // 시작점 선택
        Vector2Int startPoint = availableTiles[Random.Range(0, availableTiles.Count)];
        InsertPoint(startPoint, grid, activeList, result, minX, minY, cellSize);

        int processedCount = 0;

        while (activeList.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            
            int randomIndex = Random.Range(0, activeList.Count);
            Vector2Int currentPoint = activeList[randomIndex];
            bool foundValid = false;

            for (int attempt = 0; attempt < _settings.maxSamplingAttempts; attempt++)
            {
                // 현재 점 주변 랜덤 위치
                float angle = Random.Range(0f, Mathf.PI * 2);
                float distance = Random.Range(adjustedMinDistance, adjustedMinDistance * 2);
                
                int newX = currentPoint.x + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
                int newY = currentPoint.y + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
                Vector2Int candidate = new Vector2Int(newX, newY);

                // 유효성 검사
                if (tileSet.Contains(candidate) && IsValidPoissonPoint(candidate, grid, adjustedMinDistance, minX, minY, cellSize, gridWidth, gridHeight))
                {
                    InsertPoint(candidate, grid, activeList, result, minX, minY, cellSize);
                    foundValid = true;
                    break;
                }
            }

            if (!foundValid)
            {
                activeList.RemoveAt(randomIndex);
            }

            processedCount++;
            if (processedCount % _settings.batchSize == 0)
            {
                await UniTask.Yield(ct);
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

    private bool IsValidPoissonPoint(Vector2Int candidate, Vector2Int?[,] grid, float minDistance, int minX, int minY, float cellSize, int gridWidth, int gridHeight)
    {
        int gridX = Mathf.FloorToInt((candidate.x - minX) / cellSize);
        int gridY = Mathf.FloorToInt((candidate.y - minY) / cellSize);

        // 주변 5x5 셀 검사
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
                        float distance = Vector2Int.Distance(candidate, neighbor.Value);
                        if (distance < minDistance)
                            return false;
                    }
                }
            }
        }

        return true;
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Fisher-Yates 셔플 알고리즘
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// 가중치 기반 랜덤 선택
    /// </summary>
    private string SelectWeightedRandom(List<proportionSpawnData> items, float totalWeight)
    {
        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var item in items)
        {
            if (item == null) continue;
            
            cumulative += item.weight;
            if (randomValue <= cumulative)
            {
                return item.prefabName;
            }
        }

        return items.LastOrDefault()?.prefabName;
    }

    /// <summary>
    /// 타일 좌표를 월드 좌표로 변환
    /// </summary>
    private Vector3 TileToWorldPosition(Vector2Int tile)
    {
        // 3D 좌표로 변환 (XZ 평면)
        return new Vector3(tile.x, 0, tile.y);
    }
    #endregion
}
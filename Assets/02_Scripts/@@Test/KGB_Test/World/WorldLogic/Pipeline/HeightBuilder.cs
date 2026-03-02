using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// 높이맵 생성, 경계선 처리 등 타일별 세부 데이터를 생성하는 클래스
/// </summary>
public class HeightBuilder
{
    #region Constants
    private const float SEA_DEEP_HEIGHT = 0f;
    private const int SMOOTHING_ITERATIONS = 10;
    private const int SMOOTHING_DIRECTIONS = 8;
    private const int COASTLINE_SMOOTH_DISTANCE = 15;
    private const float COASTLINE_TARGET_HEIGHT = 0.5f;

    // 융기 관련 상수
    private const int REGION_EDGE_BUFFER = 3;
    private const float UPLIFT_EDGE_FADE_DISTANCE = 5f;

    // ★ 고원(Highlands) 전용 절벽 상수 추가
    private const float PLATEAU_CLIFF_START_DISTANCE = 4f; // 가장자리에서 얼마나 떨어져서 절벽이 시작될지
    private const float PLATEAU_CLIFF_WIDTH = 5f;          // 절벽의 경사면 너비 (작을수록 가파름)

    #endregion

    #region Fields
    private WorldSettings _worldSettings;
    private WorldGraphData _graphResult;
    private WorldLogicData _worldLogicData;
    private CancellationToken _ct;

    private static readonly int[] _dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
    private static readonly int[] _dy = { 0, 0, -1, 1, -1, 1, -1, 1 };
    #endregion

    public async UniTask<WorldLogicData> HeightBuildAsync(
        WorldGraphData result,
        WorldLogicData mapData,
        WorldSettings worldSettings,
        CancellationToken ct
        )
    {
        _graphResult = result;
        _worldSettings = worldSettings;
        _worldLogicData = mapData;
        _ct = ct;

        await InitializeHeightMapAsync();

        await GenerateHeightMapAsync();

        await SmoothCoastlineHeightAsync();

        await SmoothHeightWorldAsync();

        return _worldLogicData;
    }

    #region Phase 0: Initialize HeightMap
    private async UniTask InitializeHeightMapAsync()
    {
        for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
            {
                _worldLogicData.HeightWorld[x, y] = SEA_DEEP_HEIGHT;
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 1 : Generate Height Map
    private async UniTask GenerateHeightMapAsync()
    {
        var seedChannel = (int)WorldSeedChannel.Height_GenerateHeightMapAsync;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);

        float offsetX = (float)prng.NextDouble() * 200000f - 100000f;
        float offsetY = (float)prng.NextDouble() * 200000f - 100000f;

        // 주변 평지의 기준 높이 (고원 아래쪽 높이)
        float plainsHeight = _worldSettings.GetHeight(HeightLevel.Plains);
        float minHeight = plainsHeight / 2;

        Dictionary<RegionData, List<Vector2Int>> regionTiles = new Dictionary<RegionData, List<Vector2Int>>();
        foreach (var node in _graphResult.Nodes)
        {
            if (!regionTiles.ContainsKey(node.RegionData))
                regionTiles[node.RegionData] = new List<Vector2Int>();
            regionTiles[node.RegionData].AddRange(node.OwnedTiles);
        }

        List<RegionData> debugedRegion = new List<RegionData>();
        foreach (var kvp in regionTiles)
        {
            RegionData region = kvp.Key;
            List<Vector2Int> tiles = kvp.Value;

            // Region 내부 거리 맵 계산 (가장자리에서 얼마나 떨어져 있는지)
            Dictionary<Vector2Int, float> distanceToEdge = CalculateDistanceToRegionEdge(tiles);

            NoiseParams noiseParams = region.TerrainNoise;
            float amplitude = noiseParams.HeightVarianceBlocks;
            float regionDiameter = Mathf.Max(1f, Mathf.Sqrt(tiles.Count));

            int bumps = prng.Next((int)noiseParams.MinBumps, (int)noiseParams.MaxBumps + 1);
            float frequency = bumps / regionDiameter;

            if (!debugedRegion.Contains(region))
            {
                debugedRegion.Add(region);
                Debug.Log($"Region '{region.RegionName}' has {tiles.Count} tiles.");
            }

            // Region의 기본 높이 가져오기
            float targetBaseHeight = _worldSettings.GetHeight(region.BaseHeightLevel);

            foreach (var tile in tiles)
            {
                float xCoord = (tile.x + offsetX ) * frequency;
                float yCoord = (tile.y + offsetY) * frequency;

                // 1. 기본 노이즈 계산 (지표면의 디테일)
                float rawNoise = Mathf.PerlinNoise(xCoord, yCoord);             
                float remappedNoise = Mathf.Clamp01((rawNoise - 0.2f) / 0.6f);
                float noiseValue = remappedNoise * 2f - 1f;
                float heightNoise = noiseValue * amplitude;

                // 2. 가장자리 노이즈 페이드 (경계 부근은 노이즈를 줄여 매끄럽게 연결)
                float noiseFadeFactor = CalculateEdgeFade(distanceToEdge[tile]);
                heightNoise *= noiseFadeFactor;

                // 3. ★ 핵심: 기본 높이(BaseHeight) 결정 로직
                // 고원(Highlands)이라면 중앙만 높이고 가장자리는 깎는다.
                float finalBaseHeight = targetBaseHeight;

                if (region.BaseHeightLevel == HeightLevel.Highlands)
                {
                    // 절벽 팩터 계산 (0: 가장자리/낮음 ~ 1: 중앙/높음)
                    float plateauFactor = CalculatePlateauFactor(distanceToEdge[tile]);
                    
                    // 평지 높이에서 고원 높이로 보간 (Lerp)
                    // 가장자리 근처는 plainsHeight, 안쪽은 targetBaseHeight가 됨
                    finalBaseHeight = Mathf.Lerp(plainsHeight, targetBaseHeight, plateauFactor);
                }

                _worldLogicData.HeightWorld[tile.x, tile.y] = Mathf.Max(minHeight, finalBaseHeight + heightNoise);
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 고원(Highlands) 지형을 위한 높이 비율 계산 (절벽 만들기)
    /// </summary>
    private float CalculatePlateauFactor(float distanceToEdge)
    {
        // 1. 가장자리 버퍼 구간 (완전히 낮음)
        if (distanceToEdge < PLATEAU_CLIFF_START_DISTANCE)
        {
            return 0f;
        }

        // 2. 절벽 구간 (급격히 높아짐)
        float cliffProgress = distanceToEdge - PLATEAU_CLIFF_START_DISTANCE;
        
        // 절벽 구간을 지나면 완전히 높음
        if (cliffProgress >= PLATEAU_CLIFF_WIDTH)
        {
            return 1f;
        }

        // 0~1 사이로 정규화
        float t = cliffProgress / PLATEAU_CLIFF_WIDTH;

        // SmoothStep 등을 사용하여 S자 곡선으로 부드럽게(하지만 가파르게) 처리
        // t * t * (3f - 2f * t); // SmoothStep
        
        // 더 확실한 절벽 느낌을 위해 Power 함수 사용해도 됨
        return Mathf.Pow(t, 0.5f); // 제곱근 곡선 (초반에 빠르게 올라감 -> 둥근 절벽)
        // return t; // 선형 사면 (직선 경사)
    }

    /// <summary>
    /// Region 내 각 타일에서 Region 경계까지의 거리 계산
    /// </summary>
    private Dictionary<Vector2Int, float> CalculateDistanceToRegionEdge(List<Vector2Int> regionTiles)
    {
        Dictionary<Vector2Int, float> distanceMap = new Dictionary<Vector2Int, float>();
        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>(regionTiles);

        // ※ 최적화를 위해 매우 큰 Region의 경우 모든 타일 검색은 부하가 클 수 있음. 
        // 현재는 정확도를 위해 전체 검색 유지 하지만, 필요시 최적화 가능.
        foreach (var tile in regionTiles)
        {
            float minDistToEdge = float.MaxValue;

            // 8방향으로 경계를 찾기
            for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
            {
                int checkDist = 1;
                bool foundEdge = false;

                // 탐색 거리 제한 (너무 먼 내륙은 굳이 정밀하게 계산할 필요 없음)
                // Highlands의 절벽 계산을 위해 탐색 거리를 충분히 잡아야 함
                float maxScanDist = Mathf.Max(
                    UPLIFT_EDGE_FADE_DISTANCE + REGION_EDGE_BUFFER, 
                    PLATEAU_CLIFF_START_DISTANCE + PLATEAU_CLIFF_WIDTH + 2f
                );

                while (checkDist <= maxScanDist)
                {
                    int nx = tile.x + _dx[d] * checkDist;
                    int ny = tile.y + _dy[d] * checkDist;

                    Vector2Int neighbor = new Vector2Int(nx, ny);

                    // Region 밖이거나 다른 Region인 경우 -> 경계 발견
                    if (!tileSet.Contains(neighbor))
                    {
                        float dist = Vector2.Distance(tile, neighbor); // 유클리드 거리
                        if (dist < minDistToEdge)
                        {
                            minDistToEdge = dist;
                        }
                        foundEdge = true;
                        break;
                    }
                    checkDist++;
                }

                if (!foundEdge)
                {
                    // 탐색 범위 내에 경계가 없으면 충분히 먼 것으로 간주
                    minDistToEdge = Mathf.Min(minDistToEdge, maxScanDist);
                }
            }

            distanceMap[tile] = minDistToEdge;
        }

        return distanceMap;
    }

    /// <summary>
    /// 노이즈 억제를 위한 페이드 계수 계산 (기존 로직 유지)
    /// </summary>
    private float CalculateEdgeFade(float distanceToEdge)
    {
        if (distanceToEdge < REGION_EDGE_BUFFER)
            return 0f;

        float fadeDistance = distanceToEdge - REGION_EDGE_BUFFER;
        if (fadeDistance >= UPLIFT_EDGE_FADE_DISTANCE)
            return 1f;

        float t = fadeDistance / UPLIFT_EDGE_FADE_DISTANCE;
        return t * t * (3f - 2f * t);
    }
    #endregion

    #region Phase 2: Height Smoothing
    private async UniTask SmoothHeightWorldAsync()
    {
        for (int iteration = 0; iteration < SMOOTHING_ITERATIONS; iteration++)
        {
            float[,] nextHeightWorld = (float[,])_worldLogicData.HeightWorld.Clone();

            for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
            {
                for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
                {
                    if (_worldLogicData.TerritoryWorld[x, y] < 0) continue;

                    float sum = _worldLogicData.HeightWorld[x, y];
                    int count = 1;

                    for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                    {
                        int nx = x + _dx[d];
                        int ny = y + _dy[d];

                        if (nx >= 0 && nx < _worldLogicData.TileGridSize.x && ny >= 0 && ny < _worldLogicData.TileGridSize.y)
                        {
                            if (_worldLogicData.TerritoryWorld[nx, ny] >= 0)
                            {
                                sum += _worldLogicData.HeightWorld[nx, ny];
                                count++;
                            }
                        }
                    }
                    nextHeightWorld[x, y] = sum / count;
                }
            }
            _worldLogicData.HeightWorld = nextHeightWorld;
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 3: Coastline Height Smoothing
    private async UniTask SmoothCoastlineHeightAsync()
    {
        int[,] distanceToOcean = CalculateDistanceToOcean();

        Dictionary<RegionData, List<Vector2Int>> regionTiles = new Dictionary<RegionData, List<Vector2Int>>();
        foreach (var node in _graphResult.Nodes)
        {
            if (!regionTiles.ContainsKey(node.RegionData))
                regionTiles[node.RegionData] = new List<Vector2Int>();
            regionTiles[node.RegionData].AddRange(node.OwnedTiles);
        }

        int processedCount = 0;
        int smoothedTileCount = 0;

        foreach (var kvp in regionTiles)
        {
            RegionData region = kvp.Key;
            List<Vector2Int> tiles = kvp.Value;

            if (region.BaseHeightLevel == HeightLevel.Highlands)
                continue;

            foreach (var tile in tiles)
            {
                int x = tile.x;
                int y = tile.y;

                int distToOcean = distanceToOcean[x, y];

                if (distToOcean > 0 && distToOcean <= COASTLINE_SMOOTH_DISTANCE)
                {
                    float t = (float)distToOcean / COASTLINE_SMOOTH_DISTANCE;
                    t = t * t * (3f - 2f * t);

                    float currentHeight = _worldLogicData.HeightWorld[x, y];
                    float targetHeight = COASTLINE_TARGET_HEIGHT;

                    _worldLogicData.HeightWorld[x, y] = Mathf.Lerp(targetHeight, currentHeight, t);
                    smoothedTileCount++;
                }

                processedCount++;
                if (processedCount % 1000 == 0)
                {
                    _ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(_ct);
                }
            }
        }

        Debug.Log($"[HeightBuilder] 해안선 스무딩 완료: {smoothedTileCount}개 타일 처리됨");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    private int[,] CalculateDistanceToOcean()
    {
        int width = _worldLogicData.TileGridSize.x;
        int height = _worldLogicData.TileGridSize.y;
        int[,] distance = new int[width, height];
        bool[,] visited = new bool[width, height];

        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (_worldLogicData.TerritoryWorld[x, y] < 0)
                {
                    distance[x, y] = 0;
                    visited[x, y] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                }
                else
                {
                    distance[x, y] = int.MaxValue;
                }
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDist = distance[current.x, current.y];

            for (int d = 0; d < 4; d++)
            {
                int nx = current.x + _dx[d];
                int ny = current.y + _dy[d];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!visited[nx, ny] && _worldLogicData.TerritoryWorld[nx, ny] >= 0)
                    {
                        visited[nx, ny] = true;
                        distance[nx, ny] = currentDist + 1;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        return distance;
    }
    #endregion
}

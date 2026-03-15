using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
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
    private const float SEA_DEEP_HEIGHT = -50f;
    private const int SMOOTHING_ITERATIONS = 5;
    private const int SMOOTHING_DIRECTIONS = 8;
    private const int COASTLINE_SMOOTH_DISTANCE = 40;
    private const float COASTLINE_TARGET_HEIGHT = 0.5f;


    // 융기 관련 상수
    private const int REGION_EDGE_BUFFER = 3;               // Region 경계에서 노이즈 억제를 시작할 거리 (타일 단위)
    private const float UPLIFT_EDGE_FADE_DISTANCE = 5f;     // Region 경계에서 얼마나 멀어질 때까지 노이즈 진폭을 완전히 회복할지 (타일 단위)

    // ★ 고원 전용 절벽 상수 추가
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
        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
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

        // 지역 별로 타일을 그룹화하여 처리
        Dictionary<RegionData, List<Vector2Int>> regionTiles = new Dictionary<RegionData, List<Vector2Int>>();
        foreach (var node in _graphResult.Nodes)
        {
            if (!regionTiles.ContainsKey(node.RegionData))
                regionTiles[node.RegionData] = new List<Vector2Int>();
            regionTiles[node.RegionData].AddRange(node.OwnedTiles);
        }

        // 디버그용: 각 Region의 타일 수와 이름 출력 (중복 방지)
        List<RegionData> debugedRegion = new List<RegionData>();
        foreach (var kvp in regionTiles)
        {
            RegionData region = kvp.Key;
            List<Vector2Int> tiles = kvp.Value;

            if (!debugedRegion.Contains(region))
            {
                debugedRegion.Add(region);
                Debug.Log($"Region '{region.RegionName}' has {tiles.Count} tiles.");
            }

            // Region 내부 거리 맵 계산 (가장자리에서 얼마나 떨어져 있는지)
            Dictionary<Vector2Int, float> distanceToEdge = CalculateDistanceToRegionEdge(tiles);

            NoiseParameters noiseParams = region.TerrainNoiseParameters;
            float amplitude = noiseParams.HeightVarianceBlocks;                     // 진폭
            float regionDiameter = Mathf.Max(1f, Mathf.Sqrt(tiles.Count));          // 지역의 대략적인 지름 (노이즈 주파수 계산에 사용)
            int bumps = prng.Next((int)noiseParams.MinBumps, (int)noiseParams.MaxBumps + 1);
            float frequency = bumps / regionDiameter;

            // 옥타브 노이즈 파라미터
            int octaves = noiseParams.Octaves;
            float persistence = noiseParams.Persistence;
            float lacunarity = noiseParams.Lacunarity;
            Vector2[] octaveOffsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                // 너무 큰 값을 넣으면 PerlinNoise가 버그를 낼 수 있으므로 적절한 범위로 오프셋
                float offX = (float)prng.NextDouble() * 20000f - 10000f;
                float offY = (float)prng.NextDouble() * 20000f - 10000f;
                octaveOffsets[i] = new Vector2(offX, offY);
            }


            // Region의 기본 높이 가져오기
            float targetBaseHeight = _worldSettings.GetHeight(region.BaseHeightLevel);

            foreach (var tile in tiles)
            {
                float baseSampleX = (tile.x + offsetX) * frequency;
                float baseSampleY = (tile.y + offsetY) * frequency;

                //===========================================================
                float noiseSum = 0f;
                float maxWeight = 0f;

                float octaveWeight = 1f;
                float octaveFreq = 1f;



                // 2. 옥타브를 겹쳐서 디테일(자글자글함) 만들기
                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = baseSampleX * octaveFreq + octaveOffsets[i].x;
                    float sampleY = baseSampleY * octaveFreq + octaveOffsets[i].y;
                    float n = Mathf.PerlinNoise(sampleX, sampleY);

                    // 진폭(HeightVarianceBlocks)이 20 이상인 '산' 구역일 경우 뾰족한 능선으로 깎음
                    if (amplitude >= 30f)
                    {
                        n = 1f - Mathf.Abs(n * 2f - 1f);
                        n = n * n; // 경사를 더 가파르게
                    }

                    noiseSum += n * octaveWeight;
                    maxWeight += octaveWeight;

                    octaveWeight *= persistence; // 다음 굴곡은 영향력을 줄임
                    octaveFreq *= lacunarity;   // 다음 굴곡은 더 자잘하게 만듦
                }

                // 0.0 ~ 1.0 사이로 정규화
                float normalizedNoise = noiseSum / maxWeight;

                // 3. 평지와 산의 높이 방식 분리 (중요!)
                float noiseValue;
                if (amplitude >= 30f)
                {
                    // [산악 지대] 땅이 파이지 않고, 기준 높이에서 위로만 솟아오르게 함 (0.0 ~ 1.0)
                    noiseValue = normalizedNoise;
                }
                else
                {
                    // [평지/언덕] 기준 높이를 중심으로 위아래로 부드럽게 굽이침 (-1.0 ~ 1.0)
                    noiseValue = (normalizedNoise * 2f) - 1f;
                }

                // 4. 가장자리 감쇠 (Edge Fade) - 구역 밖으로 산이 튀어나가는 것 방지
                float distToEdge = distanceToEdge[tile];
                float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distToEdge / UPLIFT_EDGE_FADE_DISTANCE));

                // 5. ⭐️ 최종 적용: 정규화된 노이즈 형태에 유저가 설정한 진폭(amplitude)을 곱해 실제 높이를 줌
                float heightNoise = noiseValue * amplitude * edgeFade;
                float rawFinalHeight = targetBaseHeight + heightNoise;

                float influence = _worldLogicData.InfluenceWorld[tile.x, tile.y];
                float finalHeight = Mathf.Lerp(minHeight, rawFinalHeight, influence);

                if (amplitude >= 30f)
                {
                    float terraceStep = 8f;         // 계단 한 칸의 높이 (값이 클수록 거대한 층이 생김)
                    float terraceSharpness = 4.0f;  // 계단 모서리의 날카로움 (값이 클수록 직각 절벽에 가까워짐)

                    float h = finalHeight / terraceStep;     // 높이를 계단 간격으로 나눠서 몇 층인지 계산
                    float currentStep = Mathf.Floor(h);      // 정수부 (현재 층수)
                    float fractionalPart = h - currentStep;  // 소수부 (층과 층 사이의 위치 0.0 ~ 1.0)

                    // 소수부에 곡선(Smooth)을 주어 모서리가 살짝 둥근 계단을 만듭니다.
                    float smoothFraction = Mathf.Clamp01((fractionalPart - 0.5f) * terraceSharpness + 0.5f);

                    // 최종 높이를 다시 계단식으로 조립
                    finalHeight = (currentStep + smoothFraction) * terraceStep;
                }


                _worldLogicData.HeightWorld[tile.x, tile.y] = Mathf.Max(minHeight, finalHeight);
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
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


    #endregion


    /// <summary>
    ///  해안선 주변 타일의 높이를 부드럽게 낮추어 자연스러운 해안선과 섬의 윤곽을 만듭니다.
    /// </summary>
    /// <returns></returns>
    #region Phase 3: Coastline Height Smoothing
    private async UniTask SmoothCoastlineHeightAsync()
    {
        int[,] distanceToOcean = DistanceToOcean();

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

            int currentSmoothDistance = COASTLINE_SMOOTH_DISTANCE; // 기본 40

            if (region.BaseHeightLevel == HeightLevel.Highlands || region.BaseHeightLevel == HeightLevel.Mesa)
                currentSmoothDistance = 5; // 고원은 해안선이 거의 없으므로 스무딩 범위를 줄임

            foreach (var tile in tiles)
            {
                int x = tile.x;
                int y = tile.y;

                int distToOcean = distanceToOcean[x, y];

                if (distToOcean > 0 && distToOcean <= currentSmoothDistance)
                {
                    float t = (float)distToOcean / currentSmoothDistance;
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

    private int[,] DistanceToOcean()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
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

    #region Phase 4: Height Smoothing
    private async UniTask SmoothHeightWorldAsync()
    {
        float[,] readMap = _worldLogicData.HeightWorld;
        float[,] writeMap = new float[_worldLogicData.TerrainSize.x, _worldLogicData.TerrainSize.y];

        for (int iteration = 0; iteration < SMOOTHING_ITERATIONS; iteration++)
        {
            for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
            {
                for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
                {
                    if (_worldLogicData.TerritoryWorld[x, y] < 0)
                    {
                        writeMap[x, y] = readMap[x, y];
                        continue;
                    }

                    float currentHeight = readMap[x, y];
                    float sum = currentHeight;
                    int count = 1;

                    for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                    {
                        int nx = x + _dx[d];
                        int ny = y + _dy[d];

                        if (nx >= 0 && nx < _worldLogicData.TerrainSize.x && ny >= 0 && ny < _worldLogicData.TerrainSize.y)
                        {
                            float neighborHeight = readMap[nx, ny];
                            if (neighborHeight >= 0)
                            {
                                float weight = (neighborHeight < currentHeight) ? 3.0f : 0.2f;

                                sum += neighborHeight;
                                count++;

                            }
                        }
                    }
                    writeMap[x, y] = sum / count;

                }
            }
            float[,] temp = readMap;
            readMap = writeMap;
            writeMap = temp;
        }

        _worldLogicData.HeightWorld = readMap;


        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion
}

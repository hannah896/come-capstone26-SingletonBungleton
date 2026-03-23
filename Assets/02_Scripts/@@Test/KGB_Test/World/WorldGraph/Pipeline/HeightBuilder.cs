using Cysharp.Threading.Tasks;
using World.WorldGraph.Helpers.LandformStrategies;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static Unity.Cinemachine.NoiseSettings;

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

    private Dictionary<LandformType, ILandformStrategy> _heightStrategies = new()
    {
        { LandformType.Default, new DefaultStrategy() },
        { LandformType.Mountain, new MountainStrategy() },
        { LandformType.MountainRange, new MountainRangeStrategy() },
        { LandformType.Highlands, new HighlandsStrategy() },
    };

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


        // 평지의 기준 높이 
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
        foreach (var regions in regionTiles)
        {
            RegionData region = regions.Key;
            List<Vector2Int> tiles = regions.Value;

            if (!debugedRegion.Contains(region))
            {
                debugedRegion.Add(region);
                Debug.Log($"Region '{region.RegionName}' has {tiles.Count} tiles.");
            }

            // Region 내부 거리 맵 계산 (가장자리에서 얼마나 떨어져 있는지)
            

            NoiseParameters noiseParams = region.TerrainNoiseParameters;
            float amplitude = noiseParams.HeightVarianceBlocks;                     // 진폭

            float regionDiameter = Mathf.Max(1f, Mathf.Sqrt(tiles.Count));          // 지역의 대략적인 지름 (노이즈 주파수 계산에 사용)
            int bumps = prng.Next((int)noiseParams.MinBumps, (int)noiseParams.MaxBumps + 1);
            float frequency = bumps / regionDiameter;           // 맵이 넓을 수록 노이즈가 더 자주 변함.

            // 옥타브 노이즈 회수
            int octaves = noiseParams.Octaves;

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

                float distToEdge = _worldLogicData.RegionEdgeDataWorld[tile.x, tile.y].Distance; 
                float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distToEdge / UPLIFT_EDGE_FADE_DISTANCE));

                // ==========================================================
                // 1. Context 포장
                // ==========================================================
                HeightContext ctx = new HeightContext
                {
                    BaseSampleX = baseSampleX,                                      // 노이즈 샘플링을 위한 좌표
                    BaseSampleY = baseSampleY,                                      // 노이즈 샘플링을 위한 좌표
                    OctaveOffsets = octaveOffsets,                                  // 옥타브 노이즈 오프셋
                    TargetBaseHeight = targetBaseHeight,                            // 어느 정도이 높이에서 부터 융기 시작할지
                    MinHeight = minHeight,                                          // 땅의 최소 높이 (Plains의 절반)
                    DistToEdge = distToEdge,                                        // Region 경계에서의 거리
                    EdgeFade = edgeFade,                                            // 경계에서 멀어질수록 노이즈가 완전히 적용되도록 하는 페이드 값 (0~1)

                    CoastlineData= _worldLogicData.CoastlineDataWorld[tile.x, tile.y],
                    EdgeData = _worldLogicData.RegionEdgeDataWorld[tile.x, tile.y],
                    NoiseParams = noiseParams
                };

                // ==========================================================
                // 2. 전략 객체 호출 
                // ==========================================================
                if (!_heightStrategies.TryGetValue(region.LandformType, out var strategy))
                {
                    strategy = _heightStrategies[LandformType.Default];
                }

                float finalHeight = strategy.ModifyHeight(ctx);

                _worldLogicData.HeightWorld[tile.x, tile.y] = Mathf.Max(minHeight, finalHeight);
            }
        }




        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }




    #endregion


    /// <summary>
    ///  해안선 주변 타일의 높이를 부드럽게 낮추어 자연스러운 해안선과 섬의 윤곽을 만듭니다.
    /// </summary>
    /// <returns></returns>
    #region Phase 3: Coastline Height Smoothing
    private async UniTask SmoothCoastlineHeightAsync()
    {
        //int[,] distanceToOcean = DistanceToOcean();

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

            if (region.LandformType == LandformType.Highlands)
                currentSmoothDistance = 5; // 고원은 해안선이 거의 없으므로 스무딩 범위를 줄임

            foreach (var tile in tiles)
            {
                int x = tile.x;
                int y = tile.y;

                float distToOcean = _worldLogicData.CoastlineDataWorld[x, y].Distance;

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

    
    


    #endregion

    #region Phase 4: Height Smoothing
    private async UniTask SmoothHeightWorldAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;

        // 노드별 파이프라인(전략) 사전 준비 (조회 비용 최적화)
        ILandformStrategy[] nodeStrategies = new ILandformStrategy[_graphResult.Nodes.Count];
        for (int i = 0; i < _graphResult.Nodes.Count; i++)
        {
            LandformType type = _graphResult.Nodes[i].RegionData.LandformType;
            nodeStrategies[i] = _heightStrategies.ContainsKey(type) 
                ? _heightStrategies[type] 
                : _heightStrategies[LandformType.Default];
        }

        float[,] readMap = _worldLogicData.HeightWorld;
        float[,] writeMap = new float[width, height];

        for (int iteration = 0; iteration < SMOOTHING_ITERATIONS; iteration++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int ownerNode = _worldLogicData.TerritoryWorld[x, y];
                    if (ownerNode < 0)
                    {
                        writeMap[x, y] = readMap[x, y];
                        continue;
                    }

                    // 1. 해당 타일을 그리는 데 사용된 전략 꺼내기
                    ILandformStrategy strategy = nodeStrategies[ownerNode];

                    // 2. 컨텍스트 조립
                    SmoothingContext ctx = new SmoothingContext
                    {
                        X = x,
                        Y = y,
                        ReadMap = readMap,
                        MapWidth = width,
                        MapHeight = height
                    };

                    // 3. 스무딩 연산 자체를 전략 객체에 위임
                    writeMap[x, y] = strategy.SmoothHeight(ctx);
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

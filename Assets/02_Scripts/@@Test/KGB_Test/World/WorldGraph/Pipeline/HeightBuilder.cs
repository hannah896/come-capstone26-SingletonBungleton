using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using World.WorldGraph.Helpers.LandformJobs;


public enum LandformType
{
    Default,
    Mountain,
    MountainRange,
    Highlands
}
/// <summary>
/// 높이맵 생성, 경계선 처리 등 타일별 세부 데이터를 생성하는 클래스 (Job System & Burst 최적화)
/// </summary>
public class HeightBuilder : IGraphPipelineStage
{
    #region Constants
    private const float SEA_DEEP_HEIGHT = -50f;
    private const int SMOOTHING_ITERATIONS = 5;
    private const int COASTLINE_SMOOTH_DISTANCE = 40;
    private const float COASTLINE_TARGET_HEIGHT = 0.5f;

    // 융기 관련 상수
    private const float UPLIFT_EDGE_FADE_DISTANCE = 5f;
    #endregion

    #region Fields
    private WorldSettings _worldSettings;
    private WorldGraphData _graphResult;
    private WorldLogicData _worldLogicData;
    private System.Random _prng;
    private CancellationToken _ct;
    #endregion



    public void Initialize(WorldSettings settings)
    {
        _worldSettings = settings;
        _prng = new System.Random(settings.WorldSeed + (int)WorldSeedChannel.HeightBuilder);
    }

    public async UniTask ExecuteAsync(WorldGenContext ctx, CancellationToken ct)
    {
        _graphResult = ctx.GraphData;
        _worldLogicData = ctx.LogicData;
        _ct = ct;

        await InitializeHeightMapAsync();
        await GenerateHeightMapAsync();
        await SmoothCoastlineHeightAsync();
        await SmoothHeightWorldAsync();
    }

    #region Phase 0: Initialize
    private async UniTask InitializeHeightMapAsync()
    {
        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                _worldLogicData.HeightWorld[x, y] = SEA_DEEP_HEIGHT;
            }
        }
        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }
    #endregion

    #region Phase 1 : Generate Height Map (Job Optimized)
    private async UniTask GenerateHeightMapAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;
        int nodeCount = _graphResult.Nodes.Count;

        float offsetX = (float)_prng.NextDouble() * 200000f - 100000f;
        float offsetY = (float)_prng.NextDouble() * 200000f - 100000f;
        float plainsHeight = _worldSettings.GetHeight(HeightLevel.Plains);
        float minHeight = plainsHeight / 2;

        int maxOctaves = 0;
        foreach (var node in _graphResult.Nodes)
        {
            maxOctaves = Mathf.Max(maxOctaves, node.RegionData.TerrainNoiseParameters.Octaves);
        }

        NativeArray<RegionGenData> nodeRegionData = new NativeArray<RegionGenData>(nodeCount, Allocator.TempJob);
        NativeArray<float2> nodeOctaveOffsets = new NativeArray<float2>(nodeCount * maxOctaves, Allocator.TempJob);
        NativeArray<float2> edgeDataMap = new NativeArray<float2>(totalLength, Allocator.TempJob);
        NativeArray<int> territoryMap = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<float> heightMap = new NativeArray<float>(totalLength, Allocator.TempJob);

        for (int i = 0; i < nodeCount; i++)
        {
            var node = _graphResult.Nodes[i];
            var region = node.RegionData;
            var noiseParams = region.TerrainNoiseParameters;

            float regionDiameter = Mathf.Max(1f, Mathf.Sqrt(node.OwnedTiles.Count));
            int bumps = _prng.Next((int)noiseParams.MinBumps, (int)noiseParams.MaxBumps + 1);
            float frequency = bumps / regionDiameter;

            nodeRegionData[i] = new RegionGenData
            {
                TargetBaseHeight = _worldSettings.GetHeight(region.BaseHeightLevel),
                Amplitude = noiseParams.HeightVarianceBlocks,
                Frequency = frequency,
                Octaves = noiseParams.Octaves,
                Persistence = noiseParams.Persistence,
                Lacunarity = noiseParams.Lacunarity,
                LandformType = region.LandformType
            };

            for (int oct = 0; oct < noiseParams.Octaves; oct++)
            {
                float offX = (float)_prng.NextDouble() * 20000f - 10000f;
                float offY = (float)_prng.NextDouble() * 20000f - 10000f;
                nodeOctaveOffsets[i * maxOctaves + oct] = new float2(offX, offY);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                territoryMap[index] = _worldLogicData.TerritoryWorld[x, y];
                heightMap[index] = _worldLogicData.HeightWorld[x, y];
                edgeDataMap[index] = new float2(
                    _worldLogicData.RegionEdgeDataWorld[x, y].Distance,
                    _worldLogicData.RegionEdgeDataWorld[x, y].Weight
                );
            }
        }

        HeightGenerationJob job = new HeightGenerationJob
        {
            MapWidth = width,
            MapHeight = height,
            OffsetX = offsetX,
            OffsetY = offsetY,
            MinHeight = minHeight,
            UpliftEdgeFadeDistance = UPLIFT_EDGE_FADE_DISTANCE,
            MaxOctaves = maxOctaves,
            TerritoryMap = territoryMap,
            NodeRegionData = nodeRegionData,
            NodeOctaveOffsets = nodeOctaveOffsets,
            EdgeDataMap = edgeDataMap,
            HeightMap = heightMap
        };

        JobHandle handle = job.Schedule(totalLength, 64);
        while (!handle.IsCompleted) { await UniTask.Yield(_ct); }
        handle.Complete();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                _worldLogicData.HeightWorld[x, y] = heightMap[index];
            }
        }

        nodeRegionData.Dispose();
        nodeOctaveOffsets.Dispose();
        edgeDataMap.Dispose();
        territoryMap.Dispose();
        heightMap.Dispose();

        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }
    #endregion

    #region Phase 3: Coastline Height Smoothing
    private async UniTask SmoothCoastlineHeightAsync()
    {
        Dictionary<RegionData, List<Vector2Int>> regionTiles = new Dictionary<RegionData, List<Vector2Int>>();
        foreach (var node in _graphResult.Nodes)
        {
            if (!regionTiles.ContainsKey(node.RegionData))
                regionTiles[node.RegionData] = new List<Vector2Int>();
            regionTiles[node.RegionData].AddRange(node.OwnedTiles);
        }

        int processedCount = 0;
        foreach (var kvp in regionTiles)
        {
            RegionData region = kvp.Key;
            List<Vector2Int> tiles = kvp.Value;
            int currentSmoothDistance = (region.LandformType == LandformType.Highlands) ? 5 : COASTLINE_SMOOTH_DISTANCE;

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
                    _worldLogicData.HeightWorld[x, y] = Mathf.Lerp(COASTLINE_TARGET_HEIGHT, currentHeight, t);
                }

                processedCount++;
                if (processedCount % 5000 == 0) // 에디터 멈춤 방지
                {
                    _ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(_ct);
                }
            }
        }
        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }
    #endregion

    #region Phase 4: Height Smoothing (Job Optimized)
    private async UniTask SmoothHeightWorldAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int totalLength = width * height;

        NativeArray<float> readMap = new NativeArray<float>(totalLength, Allocator.TempJob);
        NativeArray<float> writeMap = new NativeArray<float>(totalLength, Allocator.TempJob);
        NativeArray<int> territoryMap = new NativeArray<int>(totalLength, Allocator.TempJob);
        NativeArray<LandformType> nodeLandformTypes = new NativeArray<LandformType>(_graphResult.Nodes.Count, Allocator.TempJob);

        for (int i = 0; i < _graphResult.Nodes.Count; i++)
        {
            nodeLandformTypes[i] = _graphResult.Nodes[i].RegionData.LandformType;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                readMap[index] = _worldLogicData.HeightWorld[x, y];
                territoryMap[index] = _worldLogicData.TerritoryWorld[x, y];
            }
        }

        for (int iteration = 0; iteration < SMOOTHING_ITERATIONS; iteration++)
        {
            HeightSmoothingJob job = new HeightSmoothingJob
            {
                ReadMap = readMap,
                TerritoryMap = territoryMap,
                NodeLandformTypes = nodeLandformTypes,
                WriteMap = writeMap,
                MapWidth = width,
                MapHeight = height
            };

            JobHandle handle = job.Schedule(totalLength, 64);
            while (!handle.IsCompleted) { await UniTask.Yield(_ct); }
            handle.Complete();

            readMap.CopyFrom(writeMap); // 더블 버퍼링 복사
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                _worldLogicData.HeightWorld[x, y] = readMap[index];
            }
        }

        readMap.Dispose();
        writeMap.Dispose();
        territoryMap.Dispose();
        nodeLandformTypes.Dispose();

        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }
    #endregion
}
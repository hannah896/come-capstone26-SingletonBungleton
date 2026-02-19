using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 높이맵 생성, 경계선 처리 등 타일별 세부 데이터를 생성하는 클래스
/// </summary>
public class DetailBuilder
{
    #region Constants
    private const float SEA_DEEP_HEIGHT = -3f;
    private const int SMOOTHING_ITERATIONS = 3;
    private const int SMOOTHING_DIRECTIONS = 4;

    public const int BORDER_MARKER = -2;
    public const int OCEAN_MARKER = -1;
    private const float OCEAN_NOISE_SCALE = 0.1f;

    #endregion

    #region Fields
    private WorldSettings _worldSettings;
    private PartitionSettings _partiSettings;
    private GraphResult _graphResult;
    private WorldMapData _worldMapData;
    private CancellationToken _ct;

    private static readonly int[] _dx = { -1, 1, 0, 0 };
    private static readonly int[] _dy = { 0, 0, -1, 1 };
    #endregion



    public async UniTask<WorldMapData> TileDetailAsync(
        GraphResult result,
        WorldMapData mapData,
        WorldSettings worldSettings,
        CancellationToken ct
        )
    {
        _graphResult = result;
        _worldSettings = worldSettings;
        _partiSettings = worldSettings.PartitionSettings;
        _worldMapData = mapData;
        _ct = ct;

        await GenerateHeightMapAsync();

        // await GenerateRampsAsync();

        await SmoothHeightWorldAsync();

        await ProcessBordersAsync();

        return _worldMapData;
    }

    #region Phase 1: Height Map Generation
    private async UniTask GenerateHeightMapAsync()
    {
        float seedOffset = (Mathf.Abs(_partiSettings.noiseSeed) % 2000) * 50f;
        float unitHeight = _worldSettings.TileUnitHeight;
        float mapArea = _worldMapData.TileGridSize.x * _worldMapData.TileGridSize.y;
        float avgAreaPerNode = mapArea / Mathf.Max(1, _graphResult.Nodes.Count);

        var regionFrequencyCache = BuildRegionFrequencyCache(avgAreaPerNode);

        for (int x = 0; x < _worldMapData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldMapData.TileGridSize.y; y++)
            {
                int nodeIndex = _worldMapData.TerritoryWorld[x, y];

                if (nodeIndex < 0)
                {
                    _worldMapData.HeightWorld[x, y] = SEA_DEEP_HEIGHT;
                    continue;
                }

                Node ownerNode = _graphResult.Nodes[nodeIndex];
                RegionData currentRegion = ownerNode.RegionData;

                float baseHeight = _worldSettings.GetHeight(ownerNode.RegionData.BaseHeightLevel);
                var noiseParams = _worldSettings.GetNoiseSettings(ownerNode.RegionData.HeightNoiseTier);

                float frequency = regionFrequencyCache[currentRegion];
                float amplitude = noiseParams.HeightVarianceBlocks * unitHeight;

                float noiseValue = Mathf.PerlinNoise((x + seedOffset) * frequency, (y + seedOffset) * frequency);
                _worldMapData.HeightWorld[x, y] = Mathf.Max(0f, baseHeight + (noiseValue * amplitude));
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    private Dictionary<RegionData, float> BuildRegionFrequencyCache(float avgAreaPerNode)
    {
        var regionNodeCounts = new Dictionary<RegionData, int>();
        foreach (var node in _graphResult.Nodes)
        {
            if (node.RegionData != null)
            {
                if (!regionNodeCounts.ContainsKey(node.RegionData)) regionNodeCounts[node.RegionData] = 0;
                regionNodeCounts[node.RegionData]++;
            }
        }

        var regionFrequencyCache = new Dictionary<RegionData, float>();
        foreach (var kvp in regionNodeCounts)
        {
            RegionData region = kvp.Key;
            float regionDiameter = Mathf.Sqrt((avgAreaPerNode * kvp.Value) / Mathf.PI) * 2f;
            var noiseParams = _worldSettings.GetNoiseSettings(region.HeightNoiseTier);

            int regionHash = region.RegionName != null ? region.RegionName.GetHashCode() : region.GetInstanceID();
            float randomT = Mathf.Abs(Mathf.Sin(regionHash * 12.9898f + _partiSettings.noiseSeed)) % 1f;

            float bumps = Mathf.Lerp(noiseParams.MinBumps, noiseParams.MaxBumps, randomT);
            regionFrequencyCache[region] = bumps / Mathf.Max(1f, regionDiameter);
        }
        return regionFrequencyCache;
    }
    #endregion

    #region Phase 3: Height Smoothing
    private async UniTask SmoothHeightWorldAsync()
    {
        for (int iteration = 0; iteration < SMOOTHING_ITERATIONS; iteration++)
        {
            float[,] nextHeightWorld = (float[,])_worldMapData.HeightWorld.Clone();

            for (int x = 0; x < _worldMapData.TileGridSize.x; x++)
            {
                for (int y = 0; y < _worldMapData.TileGridSize.y; y++)
                {
                    if (_worldMapData.TerritoryWorld[x, y] < 0) continue;

                    float sum = _worldMapData.HeightWorld[x, y];
                    int count = 1;

                    for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                    {
                        int nx = x + _dx[d];
                        int ny = y + _dy[d];

                        if (nx >= 0 && nx < _worldMapData.TileGridSize.x && ny >= 0 && ny < _worldMapData.TileGridSize.y)
                        {
                            if (_worldMapData.TerritoryWorld[nx, ny] >= 0)
                            {
                                sum += _worldMapData.HeightWorld[nx, ny];
                                count++;
                            }
                        }
                    }
                    nextHeightWorld[x, y] = sum / count;
                }
            }
            _worldMapData.HeightWorld = nextHeightWorld;
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion
    #region Phase 4: Border and Ocean Processing
    private async UniTask ProcessBordersAsync()
    {
        int processedCount = 0;
        float halfWidth = _worldMapData.TileGridSize.x * 0.5f;
        float halfHeight = _worldMapData.TileGridSize.y * 0.5f;

        for (int x = 0; x < _worldMapData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldMapData.TileGridSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();

                // 가장자리 거리 계산 최적화
                float edgeDistanceX = Mathf.Min(x, _worldMapData.TileGridSize.x - 1 - x) / halfWidth;
                float edgeDistanceY = Mathf.Min(y, _worldMapData.TileGridSize.y - 1 - y) / halfHeight;
                float edgeDistance = Mathf.Min(edgeDistanceX, edgeDistanceY);

                float oceanNoise = _worldMapData.NoiseWorld[x, y] / _partiSettings.noiseStrength * OCEAN_NOISE_SCALE;

                if (edgeDistance + oceanNoise < (1f - _partiSettings.oceanThreshold))
                {
                    _worldMapData.TerritoryWorld[x, y] = OCEAN_MARKER;
                    _worldMapData.HeightWorld[x, y] = SEA_DEEP_HEIGHT;
                }

                processedCount++;
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    await UniTask.Yield(_ct);
                }
            }
        }

        if (_partiSettings.borderWidth > 0)
        {
            await MarkBorderTilesAsync();
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    private async UniTask MarkBorderTilesAsync()
    {
        _worldMapData.BorderWorld = new int[_worldMapData.TileGridSize.x, _worldMapData.TileGridSize.y];
        //System.Array.Copy(_territoryWorld, _borderWorld, _territoryWorld.Length);

        int processedCount = 0;

        for (int x = 1; x < _worldMapData.TileGridSize.x - 1; x++)
        {
            for (int y = 1; y < _worldMapData.TileGridSize.y - 1; y++)
            {
                _ct.ThrowIfCancellationRequested();

                int currentRegion = _worldMapData.TerritoryWorld[x, y];
                if (currentRegion == OCEAN_MARKER) continue;

                bool isBorder = false;
                for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                {
                    int nx = x + _dx[d];
                    int ny = y + _dy[d];

                    if (_worldMapData.TerritoryWorld[nx, ny] != currentRegion && _worldMapData.TerritoryWorld[nx, ny] != OCEAN_MARKER)
                    {
                        isBorder = true;
                        break;
                    }
                }

                if (isBorder)
                {
                    _worldMapData.BorderWorld[x, y] = BORDER_MARKER;
                }

                processedCount++;
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    await UniTask.Yield(_ct);
                }
            }
        }
    }
    #endregion

}

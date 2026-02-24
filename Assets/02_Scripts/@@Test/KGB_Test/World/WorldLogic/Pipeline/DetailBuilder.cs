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
    private WorldGraphData _graphResult;
    private WorldLogicData _worldMapData;
    private CancellationToken _ct;

    private static readonly int[] _dx = { -1, 1, 0, 0 };
    private static readonly int[] _dy = { 0, 0, -1, 1 };
    #endregion



    public async UniTask<WorldLogicData> TileDetailAsync(
        WorldGraphData result,
        WorldLogicData mapData,
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

        await SmoothHeightWorldAsync();

        await GenerateRampsAsync();

        await ProcessBordersAsync();

        return _worldMapData;
    }

    #region Phase 1: Height Map Generation
    private async UniTask GenerateHeightMapAsync()
    {
        System.Random heightPrng = new System.Random(_worldSettings.WorldSeed + 200); 
        float offsetX = heightPrng.Next(-10000, 10000);
        float offsetY = heightPrng.Next(-10000, 10000);

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
                float amplitude = noiseParams.HeightVarianceBlocks;

                float noiseValue = Mathf.PerlinNoise((x + offsetX) * frequency, (y + offsetY) * frequency);
                _worldMapData.HeightWorld[x, y] = Mathf.Max(0f, baseHeight + ((noiseValue - 0.5f) * amplitude));
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
            float randomT = Mathf.Abs(Mathf.Sin(regionHash * 12.9898f + _worldSettings.WorldSeed)) % 1f;

            float bumps = Mathf.Lerp(noiseParams.MinBumps, noiseParams.MaxBumps, randomT);
            regionFrequencyCache[region] = bumps / Mathf.Max(1f, regionDiameter);
        }
        return regionFrequencyCache;
    }
    #endregion

    #region Phase 2: Height Smoothing
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

    #region Phase 3 : Ramp Generation 
    private async UniTask GenerateRampsAsync()
    {
        // [설정값] 지형 깎기 파라미터
        int pathRadius = 2;               // 길의 너비 (2면 폭 5칸짜리 좁은 길)
        int rampLength = 18;              // 절벽을 중심으로 양쪽으로 뻗어나갈 길이 (타일 수)
        float amplitude = 12f;            // 지그재그의 좌우 폭 (클수록 넓게 꺾임)
        float frequency = 2.5f;           // 지그재그 꺾이는 횟수
        int rampSmoothingIterations = 40; // 포크레인(스무딩) 횟수

        bool[,] isRampArea = new bool[_worldMapData.TileGridSize.x, _worldMapData.TileGridSize.y];

        foreach (var conn in _graphResult.NodeConnections)
        {
            if (conn.ParentNode.RegionData == conn.ChildNode.RegionData) continue;

            // 1. 두 구역이 맞닿는 '실제 절벽(경계선)' 픽셀들 찾기
            List<Vector2Int> boundaryPixels = new List<Vector2Int>();

            // 맵 전체를 다 뒤지면 느리니, 두 노드를 감싸는 사각형(Bounding Box) 안에서만 탐색
            int minX = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(conn.ParentNode.Position.x, conn.ChildNode.Position.x)) - 30);
            int maxX = Mathf.Min(_worldMapData.TileGridSize.x - 2, Mathf.CeilToInt(Mathf.Max(conn.ParentNode.Position.x, conn.ChildNode.Position.x)) + 30);
            int minY = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(conn.ParentNode.Position.y, conn.ChildNode.Position.y)) - 30);
            int maxY = Mathf.Min(_worldMapData.TileGridSize.y - 2, Mathf.CeilToInt(Mathf.Max(conn.ParentNode.Position.y, conn.ChildNode.Position.y)) + 30);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int r1 = _worldMapData.TerritoryWorld[x, y];
                    if (r1 < 0) continue;

                    // 내 주변 4칸 중에 다른 구역이 있는지 확인
                    for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                    {
                        int nx = x + _dx[d];
                        int ny = y + _dy[d];
                        int r2 = _worldMapData.TerritoryWorld[nx, ny];

                        if (r2 >= 0 && r1 != r2)
                        {
                            Node n1 = _graphResult.Nodes[r1];
                            Node n2 = _graphResult.Nodes[r2];

                            // 우리가 찾던 그 연결쌍이라면 경계선으로 등록
                            if ((n1 == conn.ParentNode && n2 == conn.ChildNode) ||
                                (n1 == conn.ChildNode && n2 == conn.ParentNode))
                            {
                                boundaryPixels.Add(new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }

            if (boundaryPixels.Count == 0) continue;

            // 2. 절벽의 정중앙 좌표 계산 (등산로 공사 시작점)
            Vector2 boundaryCenter = Vector2.zero;
            foreach (var p in boundaryPixels) boundaryCenter += p;
            boundaryCenter /= boundaryPixels.Count;

            // 3. 등산로가 뻗어나갈 기본 방향 (노드 -> 노드 방향)
            Vector2 dir = (conn.ChildNode.Position - conn.ParentNode.Position).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x); // 좌우로 꺾기 위한 수직 벡터

            // 4. 절벽 중심을 기준으로 -rampLength 부터 +rampLength 까지 지그재그 길 마스킹
            int steps = rampLength * 4; // 점을 촘촘하게 찍음
            for (int i = -steps; i <= steps; i++)
            {
                float t = (float)i / steps; // -1.0 ~ 1.0 사이 값

                // 양 끝으로 갈수록 흔들림(지그재그)이 줄어들어 평지와 자연스럽게 만나게 함 (Tapering)
                float taper = 1f - (t * t);
                float wiggle = Mathf.Sin(t * Mathf.PI * frequency) * amplitude * taper;

                // 중심점 + 앞뒤 전진 + 좌우 흔들림
                Vector2 pathPos = boundaryCenter + (dir * (t * rampLength)) + (perp * wiggle);

                int px = Mathf.RoundToInt(pathPos.x);
                int py = Mathf.RoundToInt(pathPos.y);

                // 길 두께(pathRadius)만큼 브러쉬로 칠하기
                for (int dx = -pathRadius; dx <= pathRadius; dx++)
                {
                    for (int dy = -pathRadius; dy <= pathRadius; dy++)
                    {
                        if (dx * dx + dy * dy <= pathRadius * pathRadius)
                        {
                            int mx = px + dx;
                            int my = py + dy;

                            if (mx >= 0 && mx < _worldMapData.TileGridSize.x && my >= 0 && my < _worldMapData.TileGridSize.y)
                            {
                                if (_worldMapData.TerritoryWorld[mx, my] >= 0)
                                {
                                    isRampArea[mx, my] = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        // 5. 마스킹된 등산로 구역만 집중적으로 깎기 (스무딩)
        int processedCount = 0;
        for (int iteration = 0; iteration < rampSmoothingIterations; iteration++)
        {
            float[,] nextHeightWorld = (float[,])_worldMapData.HeightWorld.Clone();

            for (int x = 1; x < _worldMapData.TileGridSize.x - 1; x++)
            {
                for (int y = 1; y < _worldMapData.TileGridSize.y - 1; y++)
                {
                    // ★ 등산로가 아니면 절대 건드리지 않음 (원래 절벽 보존!)
                    if (!isRampArea[x, y] || _worldMapData.TerritoryWorld[x, y] < 0) continue;

                    float sum = _worldMapData.HeightWorld[x, y];
                    int count = 1;

                    for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                    {
                        int nx = x + _dx[d];
                        int ny = y + _dy[d];

                        if (_worldMapData.TerritoryWorld[nx, ny] >= 0)
                        {
                            sum += _worldMapData.HeightWorld[nx, ny];
                            count++;
                        }
                    }
                    nextHeightWorld[x, y] = sum / count;
                }
            }
            _worldMapData.HeightWorld = nextHeightWorld;

            processedCount++;
            if (processedCount % 10 == 0) await UniTask.Yield(_ct);
        }

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

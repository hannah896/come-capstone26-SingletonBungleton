using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

//TODO: 맵 크기
/// <summary>
/// 3 단계: 보로노이 분할 및 자연스러운 경계 처리를 담당하는 클래스
/// </summary>
public class TerritoryBuilder
{
    #region Constants

    private const float DEFAULT_SEPARATION_GAP = 3.0f;
    private const int NOISE_OCTAVES = 3;
    private const float NOISE_AMPLITUDE_DECAY = 0.5f;
    private const float NOISE_FREQUENCY_GROWTH = 2f;
    public const int OCEAN_MARKER = -1;
    public const int BORDER_MARKER = -2;
    private const float OCEAN_NOISE_SCALE = 0.1f;
    private const int SMOOTHING_DIRECTIONS = 4;
    #endregion

    #region Fields
    private PartitionSettings _partiSettings;
    private WorldSettings _worldSettings;
    private WorldGraphData _graphResult;
    private WorldLogicData _worldLogicData; // ★ 데이터 바구니 추가
    private CancellationToken _ct;

    private SpatialGrid<NodeSpatialData> _nodeSpatialGrid;
    private List<NodeSpatialData> _nodeSpatialCache;                
    private static readonly int[] _dx4 = { -1, 1, 0, 0 };
    private static readonly int[] _dy4 = { 0, 0, -1, 1 };

    // ★ 추가: 8방향 (영토 테두리 등 정밀 연산용)
    private static readonly int[] _dx8 = { -1, 1, 0, 0, -1, -1, 1, 1 };
    private static readonly int[] _dy8 = { 0, 0, -1, 1, -1, 1, -1, 1 };
    // 대각선(인덱스 4~7)은 거리 1.414f, 상하좌우(인덱스 0~3)는 거리 1.0f
    private static readonly float[] _dist8 = { 1.0f, 1.0f, 1.0f, 1.0f, 1.414f, 1.414f, 1.414f, 1.414f };

    #endregion


    #region Nested Classes
    /// <summary>
    /// 노드의 공간 데이터를 캐싱하는 구조체
    /// </summary>
    private class NodeSpatialData
    {
        public int Index;
        public Vector2 Position;
        public float RegionNoiseFactor;
        public RegionData RegionData;
        public float TerritoryWeight; // ★ 추가: 영토 확장 가중치: 노드가 많은 지역에 속할수록 영토가 넓어지는 효과 (밀집 지역 확장, 고립 지역 축소)

        public NodeSpatialData(int index, Node node, float territoryWeight)
        {
            Index = index;
            Position = node.Position;
            RegionData = node.RegionData;
            // 노드별 노이즈 팩터를 미리 계산
            RegionNoiseFactor = Mathf.Sin(index * 0.7f) * 0.5f + 0.5f;
            TerritoryWeight = territoryWeight;
        }
    }
    #endregion

    /// <summary>
    /// 맵 분할 메인 파이프라인
    /// </summary>
    public async UniTask<WorldLogicData> TerritoryBuildAsync(
        WorldGraphData graphData,
        WorldLogicData logicData,
        WorldSettings worldSettings,
        CancellationToken ct)
    {
        try
        {
            _worldSettings = worldSettings;
            _partiSettings = worldSettings.PartitionSettings;
            _graphResult = graphData;
            _worldLogicData = logicData;
            _ct = ct;

            //await FitNodesToTileGridAsync();

            SpatialGrid();

            // 노이즈 맵 생성 - 경계 왜곡과 자연스러운 타일 할당을 위해(기존 보로노이 분할에 노이즈 추가)
            await GenerateNoiseWorldAsync();

            await AssignTerritoryAsync();

            await CleanUpOrphanTilesAsync();

            await ProcessBordersAsync();

            await AssignOwnedTilesToRegionsAsync();

            await GenerateInfluenceMapAsync();

            return _worldLogicData;
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Tile partitioning 이 취소되었습니다.");
            return null;
            throw;
        }
        finally
        {
            // 메모리 정리
            _nodeSpatialGrid?.Clear();
            _nodeSpatialCache?.Clear();
        }
    }



    #region Phase 1: Spatial Grid Initialization
    /// <summary>
    /// 공간 분할 그리드 초기화
    /// </summary>
    private void SpatialGrid()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        // 맵 면적 기반으로 적절한 서치 셀 크기 계산
        float mapArea = _worldLogicData.TerrainSize.x * _worldLogicData.TerrainSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        float maxTerritoryRadius = baseRadius * 2.0f;

        // 셀 크기는 최대 영역 반경의 2배로 설정 (검색 효율 최적화)
        int cellSize = Mathf.Max(10, Mathf.CeilToInt(maxTerritoryRadius * 2f));

        _nodeSpatialGrid = new SpatialGrid<NodeSpatialData>(
            _worldLogicData.TerrainSize.x,
            _worldLogicData.TerrainSize.y,
            cellSize
        );

        Dictionary<RegionData, int> regionSizes = nodes
            .Where(n => n.RegionData != null)
            .GroupBy(n => n.RegionData)
            .ToDictionary(g => g.Key, g => g.Count());

        int minRoomCount = Mathf.Max(1, regionSizes.Values.Min());

        float expansionPower = 10.0f;

        // 노드 데이터 캐싱 및 그리드에 등록
        _nodeSpatialCache = new List<NodeSpatialData>(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            int roomCount = regionSizes.ContainsKey(nodes[i].RegionData) ? regionSizes[nodes[i].RegionData] : 1;
            float ratio = (float)roomCount / minRoomCount;

            float weight = (Mathf.Sqrt(ratio) - 1f) * expansionPower;

            var spatialData = new NodeSpatialData(i, nodes[i], weight);
            _nodeSpatialCache.Add(spatialData);
            _nodeSpatialGrid.AddToNeighbors(spatialData, nodes[i].Position);
        }

        Debug.Log($"[TilePartitioner] SpatialGrid 초기화 완료 - 셀 크기: {cellSize}, 노드 수: {nodes.Count}");
    }
    #endregion

    /// <summary>
    /// 노이즈 맵 생성 - 타일별로 다중 옥타브 펄린 노이즈 계산하여 자연스러운 변형 추가
    /// </summary>
    /// <returns></returns>
    #region Phase 2: Noise Map Generation
    private async UniTask GenerateNoiseWorldAsync()
    {
        var seedChannel = (int)WorldSeedChannel.Territory_GenerateNoiseWorld;
        var prng = new System.Random(_worldSettings.WorldSeed + seedChannel);
        float offsetX = prng.Next(-10000, 10000);
        float offsetY = prng.Next(-10000, 10000);

        int processedCount = 0;

        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                float sampleX = (x + offsetX) * _partiSettings.noiseScale;
                float sampleY = (y + offsetY) * _partiSettings.noiseScale;

                // 다중 옥타브 펄린 노이즈
                float noise = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxValue = 0f;

                for (int octave = 0; octave < NOISE_OCTAVES; octave++)
                {
                    noise += Mathf.PerlinNoise(sampleX * frequency, sampleY * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= NOISE_AMPLITUDE_DECAY;
                    frequency *= NOISE_FREQUENCY_GROWTH;
                }

                _worldLogicData.NoiseWorld[x, y] = (noise / maxValue) * _partiSettings.noiseStrength;

                processedCount++;
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    _ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(_ct);
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    /// <summary>
    /// 터레인 타일에 가장 가까운 노드를 찾아서 영토 할당 - 보로노이 분할 + 노이즈 왜곡 + 연결된 지역 분리 로직 포함
    /// </summary>
    /// <returns></returns>
    #region Phase 3: AssignTerritoryAsync
    private async UniTask AssignTerritoryAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            InitializeOceanWorld();
            return;
        }

        HashSet<(RegionData, RegionData)> connectedRegions = BuildConnectedRegionsSet();

        float mapArea = _worldLogicData.TerrainSize.x * _worldLogicData.TerrainSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        float maxConnectedDist = 0f;                            
        if (_graphResult.NodeConnections != null)
        {
            foreach (NodeConnection conn in _graphResult.NodeConnections)
            {
                float dist = Vector2.Distance(conn.ParentNode.Position, conn.ChildNode.Position);
                if (dist > maxConnectedDist) maxConnectedDist = dist;
            }
        }

        // Region별 노드 수 집계
        Dictionary<RegionData, int> regionCounts = new Dictionary<RegionData, int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            RegionData regionData = nodes[i].RegionData;
            if (regionData == null) continue;
            if (!regionCounts.ContainsKey(regionData))
                regionCounts[regionData] = 0;
            regionCounts[regionData]++;
        }

        float avgNodesPerRegion = regionCounts.Count > 0 ? (float)regionCounts.Values.Average() : 1f;
        const float radiusGain = 1.15f;          // 전체 스케일 여유
        const float minRadiusScale = 0.6f;       // 지나친 축소 방지
        const float maxRadiusScale = 3.0f;       // 과도한 확장 방지

        // Region별 반경 사전
        Dictionary<RegionData, float> regionRadius = new Dictionary<RegionData, float>();
        foreach (KeyValuePair<RegionData, int> regionCount in regionCounts)
        {
            // 노드 수 기반 반경 계산 (루트 스케일링)
            float countScale = Mathf.Sqrt(regionCount.Value / Mathf.Max(1f, avgNodesPerRegion));
            float radiusFromCount = baseRadius * countScale * radiusGain;                   // 노드 수 기반 반경
            float radiusFromEdges = maxConnectedDist * 0.85f;                               // 연결된 지역 간 최대 거리 기반 반경 (밀집 지역은 작게, 드문 지역은 크게)
            float radius = Mathf.Max(radiusFromCount, radiusFromEdges, baseRadius * minRadiusScale);
            radius = Mathf.Min(radius, baseRadius * maxRadiusScale);
            regionRadius[regionCount.Key] = radius;
        }

        // 노드 인덱스별 반경 매핑
        float[] nodeRadius = new float[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            RegionData regionData = nodes[i].RegionData;
            if (regionData != null && regionRadius.TryGetValue(regionData, out float r))
                nodeRadius[i] = r;
            else
                nodeRadius[i] = baseRadius;
        }

        float separationGap = Mathf.Max(_partiSettings.noiseStrength, DEFAULT_SEPARATION_GAP);

        int processedCount = 0;
        int landCount = 0;

        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();

                Vector2 tilePos = new Vector2(x, y);

                var (idx1, dist1, idx2, dist2) = FindTopTwoNodesOptimized(tilePos, x, y);

                int finalOwner = OCEAN_MARKER;

                if (idx1 != -1 && dist1 <= nodeRadius[idx1])
                {
                    float dynamicSeparationGap = separationGap + (_worldLogicData.NoiseWorld[x, y] * 2f);

                    bool shouldSeparate = ShouldSeparateRegions(
                        idx2, dist1, dist2, separationGap,
                        nodes, connectedRegions, idx1
                    );

                    if (!shouldSeparate)
                    {
                        finalOwner = idx1;
                        landCount++;
                    }
                }

                _worldLogicData.TerritoryWorld[x, y] = finalOwner;

                processedCount++;
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    await UniTask.Yield(_ct);
                }
            }
        }

        float avgRadius = nodeRadius.Length > 0 ? nodeRadius.Average() : 0f;
        Debug.Log($"[TilePartitioner] 땅 타일: {landCount}개 / 바다 타일: {mapArea - landCount}개 | 평균 반경: {avgRadius:F2} | 최대 엣지 기반: {maxConnectedDist * 0.65f:F2}");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 모든 타일을 바다로 초기화
    /// </summary>
    private void InitializeOceanWorld()
    {
        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                _worldLogicData.TerritoryWorld[x, y] = OCEAN_MARKER;
            }
        }
    }

    /// <summary>
    /// 연결된 Region 쌍의 집합 생성
    /// </summary>
    private HashSet<(RegionData, RegionData)> BuildConnectedRegionsSet()
    {
        var connectedRegions = new HashSet<(RegionData, RegionData)>();

        foreach (var conn in _graphResult.NodeConnections)
        {
            if (conn.ParentNode.RegionData != conn.ChildNode.RegionData)
            {
                connectedRegions.Add((conn.ParentNode.RegionData, conn.ChildNode.RegionData));
                connectedRegions.Add((conn.ChildNode.RegionData, conn.ParentNode.RegionData));
            }
        }

        return connectedRegions;
    }

    /// <summary>
    /// 두 Region을 분리해야 하는지 판단
    /// </summary>
    private bool ShouldSeparateRegions(
        int idx2, float dist1, float dist2, float separationGap,
        List<Node> nodes, HashSet<(RegionData, RegionData)> connectedRegions, int idx1)
    {
        if (idx2 == -1) return false;

        float diff = dist2 - dist1;
        if (diff >= separationGap) return false;

        Node node1 = nodes[idx1];
        Node node2 = nodes[idx2];

        if (node1.RegionData == node2.RegionData) return false;

        return !connectedRegions.Contains((node1.RegionData, node2.RegionData));
    }

    /// <summary>
    /// SpatialGrid를 활용한 최근접 노드 검색
    /// </summary>
    private (int idx1, float dist1, int idx2, float dist2) FindTopTwoNodesOptimized(
        Vector2 tilePos, int x, int y)
    {
        int idx1 = -1; float dist1 = float.MaxValue;
        int idx2 = -1; float dist2 = float.MaxValue;

        // 노이즈 오프셋 가져오기
        float noiseOffset = _worldLogicData.NoiseWorld[x, y];

        // ★ 공간 그리드에서 해당 타일 주변의 노드만 검색
        var nearbyNodes = _nodeSpatialGrid.GetItemsAt(x, y);

        if (nearbyNodes == null || nearbyNodes.Count == 0)
        {
            return (idx1, dist1, idx2, dist2);
        }

        // 근처 노드들만 대상으로 거리 계산
        foreach (var spatialData in nearbyNodes)
        {
            float distance = Vector2.Distance(tilePos, spatialData.Position);

            float weightedDistance = distance - spatialData.TerritoryWeight;

            // 조기 종료 최적화
            if (distance - _partiSettings.noiseStrength > dist2) continue;

            // 노이즈가 적용된 거리
            float distortedDistance = weightedDistance + (noiseOffset * spatialData.RegionNoiseFactor);

            if (distortedDistance < dist1)
            {
                // 1등 -> 2등으로 밀려남
                dist2 = dist1;
                idx2 = idx1;

                dist1 = distortedDistance;
                idx1 = spatialData.Index;
            }
            else if (distortedDistance < dist2)
            {
                dist2 = distortedDistance;
                idx2 = spatialData.Index;
            }
        }

        return (idx1, dist1, idx2, dist2);
    }
    #endregion

    #region Phase 4: Orphan Tile Cleanup
    /// <summary>
    /// 노이즈로 인해 발생한 고립된 섬(Orphan) 타일들을 주변 영토로 병합
    /// </summary>
    private async UniTask CleanUpOrphanTilesAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;
        int[,] newTerritory = new int[width, height];

        // 원본 배열 복사해서 작업 (동시에 변경되면 계산이 꼬임 방지)
        Array.Copy(_worldLogicData.TerritoryWorld, newTerritory, _worldLogicData.TerritoryWorld.Length);

        // 8방향 탐색용 (대각선 포함)
        int[] dx8 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy8 = { -1, -1, -1, 0, 0, 1, 1, 1 };

        int cleanedCount = 0;

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                int currentOwner = _worldLogicData.TerritoryWorld[x, y];

                // 바다는 정화 대상에서 제외
                if (currentOwner == OCEAN_MARKER) continue;

                Dictionary<int, int> neighborCounts = new Dictionary<int, int>();
                int sameOwnerCount = 0;

                // 주변 8방향 조사
                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx8[i];
                    int ny = y + dy8[i];
                    int neighborOwner = _worldLogicData.TerritoryWorld[nx, ny];

                    if (neighborOwner == currentOwner)
                        sameOwnerCount++;

                    if (neighborOwner != OCEAN_MARKER)
                    {
                        if (!neighborCounts.ContainsKey(neighborOwner))
                            neighborCounts[neighborOwner] = 0;
                        neighborCounts[neighborOwner]++;
                    }
                }

                // ★ 핵심: 내 주변 8칸 중 나랑 같은 구역이 2칸 이하라면 고립된 것으로 판단
                if (sameOwnerCount <= 2 && neighborCounts.Count > 0)
                {
                    // 주변에서 가장 많이 인접한 구역을 찾아서 편입됨
                    int majorityOwner = neighborCounts.OrderByDescending(kv => kv.Value).First().Key;
                    newTerritory[x, y] = majorityOwner;
                    cleanedCount++;
                }
            }
        }

        // 정화된 배열로 덮어쓰기
        _worldLogicData.TerritoryWorld = newTerritory;
        Debug.Log($"[TerritoryBuilder] 알박기 타일 정화 완료: {cleanedCount}개 타일 수정됨");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    #endregion

    #region Phase 5: Border and Ocean Processing
    private async UniTask ProcessBordersAsync()
    {
        int processedCount = 0;
        float halfWidth = _worldLogicData.TerrainSize.x * 0.5f;
        float halfHeight = _worldLogicData.TerrainSize.y * 0.5f;

        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();

                // 가장자리 거리 계산 최적화
                float edgeDistanceX = Mathf.Min(x, _worldLogicData.TerrainSize.x - 1 - x) / halfWidth;
                float edgeDistanceY = Mathf.Min(y, _worldLogicData.TerrainSize.y - 1 - y) / halfHeight;
                float edgeDistance = Mathf.Min(edgeDistanceX, edgeDistanceY);

                float oceanNoise = _worldLogicData.NoiseWorld[x, y] / _partiSettings.noiseStrength * OCEAN_NOISE_SCALE;

                if (edgeDistance + oceanNoise < (1f - _partiSettings.oceanThreshold))
                {
                    _worldLogicData.TerritoryWorld[x, y] = OCEAN_MARKER;
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
        _worldLogicData.BorderWorld = new int[_worldLogicData.TerrainSize.x, _worldLogicData.TerrainSize.y];
        //System.Array.Copy(_territoryWorld, _borderWorld, _territoryWorld.Length);

        int processedCount = 0;

        for (int x = 1; x < _worldLogicData.TerrainSize.x - 1; x++)
        {
            for (int y = 1; y < _worldLogicData.TerrainSize.y - 1; y++)
            {
                _ct.ThrowIfCancellationRequested();

                int currentRegion = _worldLogicData.TerritoryWorld[x, y];
                if (currentRegion == OCEAN_MARKER) continue;

                bool isBorder = false;
                for (int d = 0; d < SMOOTHING_DIRECTIONS; d++)
                {
                    int nx = x + _dx4[d];
                    int ny = y + _dy4[d];

                    if (_worldLogicData.TerritoryWorld[nx, ny] != currentRegion && _worldLogicData.TerritoryWorld[nx, ny] != OCEAN_MARKER)
                    {
                        isBorder = true;
                        break;
                    }
                }

                if (isBorder)
                {
                    _worldLogicData.BorderWorld[x, y] = BORDER_MARKER;
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
    
    #region Phase 6: Assign Tiles to Regions
    /// <summary>
    /// 타일 데이터를 각 노드의 RegionData에 할당하여 소유 타일 목록 구축  
    /// </summary>
    /// <returns></returns>
    private async UniTask AssignOwnedTilesToRegionsAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            node.OwnedTiles.Clear();
        }

        int processedCount = 0;

        for (int x = 0; x < _worldLogicData.TerrainSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TerrainSize.y; y++)
            {
                int nodeIndex = _worldLogicData.TerritoryWorld[x, y];       // 타일에 할당된 노드 인덱스
                if (nodeIndex >= 0 && nodeIndex < nodes.Count)
                {
                    nodes[nodeIndex].OwnedTiles.Add(new Vector2Int(x, y));
                }

                processedCount++;
                if (processedCount % (_partiSettings.batchSize * 5) == 0)
                {
                    _ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(_ct);
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 7: Influence Map Generation
    private async UniTask GenerateInfluenceMapAsync()
    {
        int width = _worldLogicData.TerrainSize.x;
        int height = _worldLogicData.TerrainSize.y;

        // Node ID로 RegionData를 빠르게 찾기 위한 매핑 배열
        RegionData[] nodeToRegion = _graphResult.Nodes.Select(n => n.RegionData).ToArray();
        // 해안선(Ocean) 까지의 거리 계산용 데이터
        int[,] distanceToOcean = new int[width, height];
        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> queueOcean = new Queue<Vector2Int>();

        // 영토 경계선(Edge) 까지의 거리 계산용 데이터
        float[,] distanceToEdge = new float[width, height];
        bool[,] visitedEdge = new bool[width, height];
        Queue<Vector2Int> queueEdge = new Queue<Vector2Int>();


        // 1. 바다(OCEAN_MARKER) 타일 큐에 넣기
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int ownerNode = _worldLogicData.TerritoryWorld[x, y];
                if (ownerNode == OCEAN_MARKER)
                {
                    distanceToOcean[x, y] = 0;
                    visited[x, y] = true;
                    queueOcean.Enqueue(new Vector2Int(x, y));
                    distanceToEdge[x, y] = 0f;
                }
                else
                {
                    distanceToOcean[x, y] = int.MaxValue;
                    distanceToEdge[x, y] = float.MaxValue;

                    RegionData myRegion = nodeToRegion[ownerNode];
                    bool isBorder = false;
                    // ★ Fields의 8방향 상수 사용
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + _dx8[d];
                        int ny = y + _dy8[d];
                        // 맵 밖이거나
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        {
                            isBorder = true;
                            break;
                        }

                        int neighborNode = _worldLogicData.TerritoryWorld[nx, ny];
                        // 이웃이 바다이거나, "소속 Region"이 다르면 경계선! (같은 Region 내의 다른 Node는 무시)
                        if (neighborNode == OCEAN_MARKER || nodeToRegion[neighborNode] != myRegion)
                        {
                            isBorder = true;
                            break;
                        }
                    }

                    if (isBorder)
                    {
                        distanceToEdge[x, y] = 0f;
                        visitedEdge[x, y] = true;
                        queueEdge.Enqueue(new Vector2Int(x, y));
                    }
                }
            }
        }

        // 2-1 . BFS로 해안선 거리 계산 (육지 전체의 뼈대 잡기)
        while (queueOcean.Count > 0)
        {
            Vector2Int current = queueOcean.Dequeue();
            int currentDist = distanceToOcean[current.x, current.y];

            for (int d = 0; d < 4; d++)
            {
                int nx = current.x + _dx4[d];
                int ny = current.y + _dy4[d];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!visited[nx, ny] && _worldLogicData.TerritoryWorld[nx, ny] >= 0)
                    {
                        visited[nx, ny] = true;
                        distanceToOcean[nx, ny] = currentDist + 1;
                        queueOcean.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
        // 2-2. BFS로 영토 경계선까지의 거리 계산 (영토 내부의 세밀한 영향력 계산용)
        while (queueEdge.Count>0)
        {
            Vector2Int current = queueEdge.Dequeue();
            float currentDist = distanceToEdge[current.x, current.y];

            int myOwner = _worldLogicData.TerritoryWorld[current.x, current.y];
            RegionData myRegion = nodeToRegion[myOwner];

            for (int d = 0; d < 8; d++)
            {
                int nx = current.x + _dx8[d]; // ★ Fields 상수
                int ny = current.y + _dy8[d]; // ★ Fields 상수

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!visitedEdge[nx, ny])
                    {
                        int neighborNode = _worldLogicData.TerritoryWorld[nx, ny];
                        if (neighborNode != OCEAN_MARKER)
                        {
                            RegionData neighborRegion = nodeToRegion[neighborNode];

                            // 같은 Region 이면 거리를 전파하며 통과합니다!
                            if (neighborRegion == myRegion)
                            {
                                visitedEdge[nx, ny] = true;
                                distanceToEdge[nx, ny] = currentDist + _dist8[d];
                                queueEdge.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }
                }
            }
        }
        // ==========================================================
        // 3. [통합 for문] 각 영토별 최대 깊이(MaxDepth) 구하기
        // ==========================================================
        Dictionary<int, int> regionMaxOceanDepth = new Dictionary<int, int>();
        Dictionary<int, float> regionMaxEdgeDepth = new Dictionary<int, float>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int owner = _worldLogicData.TerritoryWorld[x, y];
                if (owner >= 0)
                {
                    // 바다 깊이 갱신
                    if (!regionMaxOceanDepth.ContainsKey(owner)) regionMaxOceanDepth[owner] = 1;
                    if (distanceToOcean[x, y] > regionMaxOceanDepth[owner])
                        regionMaxOceanDepth[owner] = distanceToOcean[x, y];

                    // 경계선 깊이 갱신
                    if (!regionMaxEdgeDepth.ContainsKey(owner)) regionMaxEdgeDepth[owner] = 1f;
                    if (distanceToEdge[x, y] > regionMaxEdgeDepth[owner])
                        regionMaxEdgeDepth[owner] = distanceToEdge[x, y];
                }
            }
        }

        // ==========================================================
        // 4. [통합 for문] 바구니에 담기 & 정규화(0~1)
        // ==========================================================
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int owner = _worldLogicData.TerritoryWorld[x, y];
                _worldLogicData.DistanceToOceanWorld[x, y] = distanceToOcean[x, y];

                if (owner >= 0)     // 육지 타일인 경우
                {
                    // 4-1. 전역 영향력 (바다 기준)
                    float currentMaxOceanDepth = Mathf.Max(1f, regionMaxOceanDepth[owner]);
                    float coastPercentage = (float)distanceToOcean[x, y] / currentMaxOceanDepth;
                    _worldLogicData.CoastlineDataWorld[x, y] = new InfluenceData
                    {
                        Distance = distanceToOcean[x, y],
                        Weight = Mathf.SmoothStep(0.1f, 1.0f, Mathf.Clamp01(coastPercentage))
                    };


                    // 4-2. 지역 영향력 (영토 경계선 기준) ★ 추가된 데이터
                    float currentMaxEdgeDepth = Mathf.Max(1f, regionMaxEdgeDepth[owner]);
                    float edgePercentage = distanceToEdge[x, y] / currentMaxEdgeDepth;
                    _worldLogicData.RegionEdgeDataWorld[x, y] = new InfluenceData
                    {
                        Distance = distanceToEdge[x, y],
                        Weight = Mathf.SmoothStep(0.1f, 1.0f, Mathf.Clamp01(edgePercentage))
                    };
                }
                else
                {
                    _worldLogicData.CoastlineDataWorld[x, y] = new InfluenceData { Distance = 0f, Weight = 0f };
                    _worldLogicData.RegionEdgeDataWorld[x, y] = new InfluenceData { Distance = 0f, Weight = 0f };
                }
            }
        }


        Debug.Log("[TerritoryBuilder] 영토 맞춤형 영향력 맵 생성 완료");
        if (_worldSettings.EnableStepByStep) await UniTask.Yield(_ct);
    }

    #endregion
}
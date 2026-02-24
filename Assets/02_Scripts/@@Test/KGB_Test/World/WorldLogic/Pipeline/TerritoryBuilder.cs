using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 3 단계: 보로노이 분할 및 자연스러운 경계 처리를 담당하는 클래스
/// </summary>
public class TerritoryBuilder
{
    #region Constants
    private const float DEFAULT_PADDING = 0.1f;
    private const float MIN_DIMENSION_SIZE = 0.1f;
    private const float DEFAULT_SEPARATION_GAP = 3.0f;
    private const int NOISE_OCTAVES = 3;
    private const float NOISE_AMPLITUDE_DECAY = 0.5f;
    private const float NOISE_FREQUENCY_GROWTH = 2f;
    public const int OCEAN_MARKER = -1;

    #endregion

    #region Fields
    private PartitionSettings _partiSettings;
    private WorldSettings _worldSettings;
    private WorldGraphData _graphResult;
    private WorldLogicData _worldLogicData; // ★ 데이터 바구니 추가
    private CancellationToken _ct;

    private SpatialGrid<NodeSpatialData> _nodeSpatialGrid;
    private List<NodeSpatialData> _nodeSpatialCache;
    private static readonly int[] _dx = { -1, 1, 0, 0 };
    private static readonly int[] _dy = { 0, 0, -1, 1 };
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

        public NodeSpatialData(int index, Node node)
        {
            Index = index;
            Position = node.Position;
            RegionData = node.RegionData;
            // 노드별 노이즈 팩터를 미리 계산
            RegionNoiseFactor = Mathf.Sin(index * 0.7f) * 0.5f + 0.5f;
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

            await FitNodesToTileGridAsync();

            InitializeSpatialGrid();

            await GenerateNoiseWorldAsync();

            await AssignTerritoriesAsync();

            await AssignOwnedTilesToRegionsAsync();


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

    #region Phase 0: Spatial Grid Initialization
    /// <summary>
    /// 공간 분할 그리드 초기화
    /// </summary>
    private void InitializeSpatialGrid()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        // 맵 면적 기반으로 적절한 셀 크기 계산
        float mapArea = _worldLogicData.TileGridSize.x * _worldLogicData.TileGridSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        float maxTerritoryRadius = baseRadius * 2.0f;

        // 셀 크기는 최대 영역 반경의 2배로 설정 (검색 효율 최적화)
        int cellSize = Mathf.Max(10, Mathf.CeilToInt(maxTerritoryRadius * 2f));

        _nodeSpatialGrid = new SpatialGrid<NodeSpatialData>(
            _worldLogicData.TileGridSize.x,
            _worldLogicData.TileGridSize.y,    
            cellSize
        );

        // 노드 데이터 캐싱 및 그리드에 등록
        _nodeSpatialCache = new List<NodeSpatialData>(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            var spatialData = new NodeSpatialData(i, nodes[i]);
            _nodeSpatialCache.Add(spatialData);

            // 경계 문제 방지를 위해 인접 셀에도 등록
            _nodeSpatialGrid.AddToNeighbors(spatialData, nodes[i].Position);
        }

        Debug.Log($"[TilePartitioner] SpatialGrid 초기화 완료 - 셀 크기: {cellSize}, 노드 수: {nodes.Count}");
    }
    #endregion

    #region Phase 0: Fit Nodes to Grid
    private async UniTask FitNodesToTileGridAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0) return;

        // 1. 현재 노드들의 범위(Bounds) 계산
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var node in nodes)
        {
            if (node.Position.x < minX) minX = node.Position.x;
            if (node.Position.x > maxX) maxX = node.Position.x;
            if (node.Position.y < minY) minY = node.Position.y;
            if (node.Position.y > maxY) maxY = node.Position.y;
        }

        // 2. 현재 그래프의 크기
        float currentWidth = maxX - minX;
        float currentHeight = maxY - minY;

        if (currentWidth < MIN_DIMENSION_SIZE) currentWidth = 1f;
        if (currentHeight < MIN_DIMENSION_SIZE) currentHeight = 1f;

        // 3. 목표 월드 크기 (가장자리에 여백 둠)
        float targetWidth = _worldLogicData.TileGridSize.x * (1f - DEFAULT_PADDING * 2);
        float targetHeight = _worldLogicData.TileGridSize.y * (1f - DEFAULT_PADDING * 2);

        // 4. 스케일 비율 계산 (비율 유지하면서 꽉 차게)
        float scaleX = targetWidth / currentWidth;
        float scaleY = targetHeight / currentHeight;
        float finalScale = Mathf.Min(scaleX, scaleY);

        // 5. 중심점 이동 계산
        Vector2 currentCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 targetCenter = new Vector2(_worldLogicData.TileGridSize.x * 0.5f, _worldLogicData.TileGridSize.y * 0.5f);

        // 6. 좌표 변환 적용
        foreach (var node in nodes)
        {
            Vector2 relativePos = node.Position - currentCenter;
            node.Position = targetCenter + (relativePos * finalScale);
        }

        // 7. 검증
        ValidateNodePositions(nodes);

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 노드 위치 검증
    /// </summary>
    private void ValidateNodePositions(List<Node> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;

        bool hasInvalidNode = false;
        foreach (var node in nodes)
        {
            if (node.Position.x < 0 || node.Position.x > _worldLogicData.TileGridSize.x ||
                node.Position.y < 0 || node.Position.y > _worldLogicData.TileGridSize.y)
            {
                Debug.LogError($"🚨 노드가 맵 범위를 벗어남: {node.Position} (맵 크기: {_worldLogicData.TileGridSize})");
                hasInvalidNode = true;
            }
        }

        if (!hasInvalidNode)
        {
            Debug.Log($"✅ 모든 노드가 맵 범위 내에 배치됨 (첫 노드: {nodes[0].Position})");
        }
    }
    #endregion

    #region Phase 1: Noise Map Generation
    private async UniTask GenerateNoiseWorldAsync()
    {
        System.Random prng = new System.Random(_worldSettings.WorldSeed + 100);
        float offsetX = prng.Next(-10000, 10000);
        float offsetY = prng.Next(-10000, 10000);

        int processedCount = 0;

        for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
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

    #region Phase 2: Voronoi Partitioning with Noise
    private async UniTask AssignTerritoriesAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            InitializeOceanWorld();
            return;
        }

        HashSet<(RegionData, RegionData)> connectedRegions = BuildConnectedRegionsSet();

        float mapArea = _worldLogicData.TileGridSize.x * _worldLogicData.TileGridSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;
        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        //float maxTerritoryRadius = baseRadius * 2.0f;

        // ==========================================
        float maxConnectedDist = 0f;
        if (_graphResult.NodeConnections != null)
        {
            foreach (var conn in _graphResult.NodeConnections)
            {
                float dist = Vector2.Distance(conn.ParentNode.Position, conn.ChildNode.Position);
                if (dist > maxConnectedDist) maxConnectedDist = dist;
            }
        }

        // 최대 엣지 길이의 65% 만큼 뻗어나가도록 설정
        float maxTerritoryRadius = Mathf.Max(baseRadius * 2.0f, maxConnectedDist * 0.65f);
        // ==========================================

        float separationGap = Mathf.Max(_partiSettings.noiseStrength, DEFAULT_SEPARATION_GAP);

        int processedCount = 0;
        int landCount = 0;

        for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();

                Vector2 tilePos = new Vector2(x, y);

                var (idx1, dist1, idx2, dist2) = FindTopTwoNodesOptimized(tilePos, x, y);

                int finalOwner = OCEAN_MARKER;

                if (idx1 != -1 && dist1 <= maxTerritoryRadius)
                {
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

        Debug.Log($"[TilePartitioner] 땅 타일: {landCount}개 / 바다 타일: {mapArea - landCount}개 | 최대 반경: {maxTerritoryRadius:F2}");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 모든 타일을 바다로 초기화
    /// </summary>
    private void InitializeOceanWorld()
    {
        for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
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
    /// ★ [최적화] SpatialGrid를 활용한 최근접 노드 검색
    /// O(n) -> O(log n) 수준으로 개선
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

            // 조기 종료 최적화
            if (distance - _partiSettings.noiseStrength > dist2) continue;

            // 노이즈가 적용된 거리
            float distortedDistance = distance + (noiseOffset * spatialData.RegionNoiseFactor);

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


   

    #region Phase 5: Assign Tiles to Regions
    private async UniTask AssignOwnedTilesToRegionsAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            node.OwnedTiles.Clear();
        }

        int processedCount = 0;

        for (int x = 0; x < _worldLogicData.TileGridSize.x; x++)
        {
            for (int y = 0; y < _worldLogicData.TileGridSize.y; y++)
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


}
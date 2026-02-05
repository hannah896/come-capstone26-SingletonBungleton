using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 3-4단계: 보로노이 분할 및 자연스러운 경계 처리를 담당하는 클래스
/// </summary>
public class TilePartitioner
{
    

    private PartitionSettings _partiSettings;
    private WorldSettings _worldSettings;
    private GraphResult _graphResult;
    private CancellationToken _ct;
    private Vector2Int _worldSize;
    private int[,] _territoryWorld;    // 각 타일이 어느 region에 속하는지 (-1: 바다/벽)
    private int[,] _borderWorld;       // 경계 타일 정보 (-2: 경계)
    private float[,] _noiseWorld;      // 펄린 노이즈 캐시

    public int[,] TerritoryWorld => _territoryWorld;
    public int[,] BorderWorld => _borderWorld;

    /// <summary>
    /// 맵 분할 메인 파이프라인
    /// </summary>
    public async UniTask PartitionWorldAsync(
        GraphResult result,
        WorldSettings worldSettings,
        CancellationToken ct)
    {
        try
        {
            _worldSettings = worldSettings;
            _partiSettings = worldSettings.PartitionSettings;
            _graphResult = result;
            _ct = ct;
            _worldSize = _worldSettings.GetWorldSize();
            _territoryWorld = new int[_worldSize.x, _worldSize.y];

            // 들어온 노드들을 맵에 맞게 조정
            await FitNodesToGridAsync();

            // Phase 1: 노이즈 맵 생성
            await GenerateNoiseWorldAsync();

            // Phase 2: 보로노이 분할 (노이즈 적용)
            await AssignTerritoriesAsync();

            // Phase 3: 경계 및 바다 처리
            await ProcessBordersAsync();

            // Phase 4: 각 Region에 소유 타일 할당
            await AssignOwnedTilesToRegionsAsync();
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Tile partitioning 이 취소되었습니다.");
            throw;
        }
    }

    #region Phase 0: Fit Nodes to Grid
    private async UniTask FitNodesToGridAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null) return;

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

        if (currentWidth < 0.1f) currentWidth = 1f;
        if (currentHeight < 0.1f) currentHeight = 1f;

        // 3. 목표 월드 크기 (가장자리에 10% 여백 둠)
        float padding = 0.1f;
        float targetWidth = _worldSize.x * (1f - padding * 2);
        float targetHeight = _worldSize.y * (1f - padding * 2);

        // 4. 스케일 비율 계산 (비율 유지하면서 꽉 차게)
        float scaleX = targetWidth / currentWidth;
        float scaleY = targetHeight / currentHeight;
        float finalScale = Mathf.Min(scaleX, scaleY);

        // 5. 중심점 이동 계산
        Vector2 currentCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        Vector2 targetCenter = new Vector2(_worldSize.x / 2f, _worldSize.y / 2f);

        // 6. 좌표 변환 적용
        foreach (var node in nodes)
        {
            // 원점 기준으로 이동 -> 스케일링 -> 목표 중앙으로 이동
            Vector2 relativePos = node.Position - currentCenter;
            node.Position = targetCenter + (relativePos * finalScale);
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 1: Noise Map Generation
    private async UniTask GenerateNoiseWorldAsync()
    {
        _noiseWorld = new float[_worldSize.x, _worldSize.y];
        float offsetX = _partiSettings.noiseSeed * 100f;
        float offsetY = _partiSettings.noiseSeed * 100f;

        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                float sampleX = (x + offsetX) * _partiSettings.noiseScale;
                float sampleY = (y + offsetY) * _partiSettings.noiseScale;
                
                // 다중 옥타브 펄린 노이즈
                float noise = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxValue = 0f;
                
                for (int octave = 0; octave < 3; octave++)
                {
                    noise += Mathf.PerlinNoise(sampleX * frequency, sampleY * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }
                
                _noiseWorld[x, y] = (noise / maxValue) * _partiSettings.noiseStrength;
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
            // 모든 타일을 -1 (바다/벽)로 초기화
            for (int x = 0; x < _worldSize.x; x++)
                for (int y = 0; y < _worldSize.y; y++)
                    _territoryWorld[x, y] = -1;
            return;
        }

        float mapArea = _worldSize.x * _worldSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;

        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);

        float maxTerritoryRadius = baseRadius * 1.4f;

        int processedCount = 0;

        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();
                
                Vector2 tilePos = new Vector2(x, y);

                var (closestRegionIndex, dist) = FindClosestNodeWithNoise(tilePos, nodes);
                _territoryWorld[x, y] = closestRegionIndex;

                if (dist <= maxTerritoryRadius)
                {
                    _territoryWorld[x, y] = closestRegionIndex;
                }
                else
                {
                    _territoryWorld[x, y] = -1; // 너무 멀면 바다(Ocean)
                }

                processedCount++;
                // 배치 처리로 메인 스레드 양보
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    await UniTask.Yield(_ct);
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 노이즈가 적용된 최근접 노드 인덱스 찾기
    /// </summary>
    private (int index, float distance) FindClosestNodeWithNoise(Vector2 tilePos, List<Node> nodes)
    {
        int closestIndex = -1;
        float minDistance = float.MaxValue;

        int x = Mathf.Clamp((int)tilePos.x, 0, _worldSize.x - 1);
        int y = Mathf.Clamp((int)tilePos.y, 0, _worldSize.y - 1);
        float noiseOffset = _noiseWorld[x, y];

        for(int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            
            float distance = Vector2.Distance(tilePos, node.Position);
            
            // 조기 종료: 노이즈 최대값을 더해도 현재 최소값보다 크면 스킵
            if (distance - _partiSettings.noiseStrength > minDistance)
                continue;
            
            // 각 영역별로 다른 노이즈 오프셋 적용 (영역 고유성)
            float regionNoiseFactor = Mathf.Sin(i * 0.7f) * 0.5f + 0.5f;
            float distortedDistance = distance + noiseOffset * regionNoiseFactor;
            
            if (distortedDistance < minDistance)
            {
                minDistance = distortedDistance;
                closestIndex = i;
            }
        }

        return (closestIndex, minDistance);
    }
    #endregion

    #region Phase 3: Border and Ocean Processing
    private async UniTask ProcessBordersAsync()
    {
        int processedCount = 0;

        // 맵 가장자리를 바다/벽으로 처리
        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();
                
                // 가장자리 거리 계산 (0~1, 1이 중앙)
                float edgeDistanceX = Mathf.Min(x, _worldSize.x - 1 - x) / (float)(_worldSize.x * 0.5f);
                float edgeDistanceY = Mathf.Min(y, _worldSize.y - 1 - y) / (float)(_worldSize.y * 0.5f);
                float edgeDistance = Mathf.Min(edgeDistanceX, edgeDistanceY);
                
                // 노이즈로 해안선 불규칙하게
                float oceanNoise = _noiseWorld[x, y] / _partiSettings.noiseStrength * 0.1f;
                
                if (edgeDistance + oceanNoise < (1f - _partiSettings.oceanThreshold))
                {
                    _territoryWorld[x, y] = -1; // 바다/벽
                }
                
                processedCount++;
                if (processedCount % _partiSettings.batchSize == 0)
                {
                    await UniTask.Yield(_ct);
                }
            }
        }

        // 영역 간 경계선 처리 (옵션)
        if (_partiSettings.borderWidth > 0)
        {
            await MarkBorderTilesAsync();
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 영역 간 경계 타일 마킹
    /// </summary>
    private async UniTask MarkBorderTilesAsync()
    {
        _borderWorld = new int[_worldSize.x, _worldSize.y];
        System.Array.Copy(_territoryWorld, _borderWorld, _territoryWorld.Length);

        int processedCount = 0;
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int x = 1; x < _worldSize.x - 1; x++)
        {
            for (int y = 1; y < _worldSize.y - 1; y++)
            {
                _ct.ThrowIfCancellationRequested();
                
                int currentRegion = _territoryWorld[x, y];
                if (currentRegion == -1) continue;

                // 인접 타일 검사
                bool isBorder = false;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d];
                    int ny = y + dy[d];
                    
                    if (_territoryWorld[nx, ny] != currentRegion && _territoryWorld[nx, ny] != -1)
                    {
                        isBorder = true;
                        break;
                    }
                }

                // 경계 타일은 -2로 마킹 (나중에 벽 타일로 사용 가능)
                if (isBorder)
                {
                    _borderWorld[x, y] = -2;
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

    #region Phase 4: Assign Tiles to Regions
    private async UniTask AssignOwnedTilesToRegionsAsync()
    {
        var nodes = _graphResult.Nodes;
        if (nodes == null) return;
        // 각 Region의 소유 타일 초기화
        foreach (var node in nodes)
        {
            node.OwnedTiles.Clear();
        }

        int processedCount = 0;
        // 타일 할당
        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                int nodeIndex = _territoryWorld[x, y];
                if (nodeIndex >= 0 && nodeIndex < nodes.Count)
                {
                    nodes[nodeIndex].OwnedTiles.Add(new Vector2Int(x, y));
                }
                processedCount++;
                if (processedCount % (_partiSettings.batchSize * 5) == 0) // 단순 대입이라 덜 자주 해도 됨
                {
                    await UniTask.Yield(_ct);
                }
            }
        }

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 특정 좌표가 어느 영역에 속하는지 반환
    /// </summary>
    public int GetRegionAt(int x, int y)
    {
        if (x < 0 || x >= _worldSize.x || y < 0 || y >= _worldSize.y)
            return -1;
        return _territoryWorld[x, y];
    }

    /// <summary>
    /// 특정 좌표가 유효한 땅인지 확인
    /// </summary>
    public bool IsValidLand(int x, int y)
    {
        return GetRegionAt(x, y) >= 0;
    }

    /// <summary>
    /// 특정 좌표가 경계인지 확인
    /// </summary>
    public bool IsBorder(int x, int y)
    {
        if (_borderWorld == null) return false;
        if (x < 0 || x >= _worldSize.x || y < 0 || y >= _worldSize.y)
            return false;
        return _borderWorld[x, y] == -2;
    }
    #endregion
}
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
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
    private Vector2Int _tileGridSize;
    private int[,] _territoryWorld;    // 각 타일이 어느 region에 속하는지 (-1: 바다/벽)
    private float[,] _heightWorld;      // 추가: 높이 맵
    private int[,] _borderWorld;       // 경계 타일 정보 (-2: 경계)
    private float[,] _noiseWorld;      // 펄린 노이즈 캐시

    public int[,] TerritoryWorld => _territoryWorld;
    public float[,] HeightWorld => _heightWorld;
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

            // 1. 사이즈 정보 가져오기
            _tileGridSize = _worldSettings.GetTileGridSize();         // 예: 500
            _territoryWorld = new int[_tileGridSize.x, _tileGridSize.y];

            // 들어온 노드들을 맵에 맞게 조정
            await FitNodesToTileGridAsync();

            // Phase 1: 노이즈 맵 생성
            await GenerateNoiseWorldAsync();

            // Phase 2: 보로노이 분할 (노이즈 적용)
            await AssignTerritoriesAsync();

            // Phase 3: 높이 맵 생성
            await GenerateHeightMapAsync();

            // Phase 4: 경계 및 바다 처리
            await ProcessBordersAsync();

            // Phase 5: 각 Region에 소유 타일 할당
            await AssignOwnedTilesToRegionsAsync();
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Tile partitioning 이 취소되었습니다.");
            throw;
        }
    }

    #region Phase 0: Fit Nodes to Grid
    private async UniTask FitNodesToTileGridAsync()
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
        float targetWidth = _tileGridSize.x * (1f - padding * 2);
        float targetHeight = _tileGridSize.y * (1f - padding * 2);

        // 4. 스케일 비율 계산 (비율 유지하면서 꽉 차게)
        float scaleX = targetWidth / currentWidth;
        float scaleY = targetHeight / currentHeight;
        float finalScale = Mathf.Min(scaleX, scaleY);

        // 5. 중심점 이동 계산
        Vector2 currentCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        Vector2 targetCenter = new Vector2(_tileGridSize.x / 2f, _tileGridSize.y / 2f);

        // 6. 좌표 변환 적용
        foreach (var node in nodes)
        {
            // 원점 기준으로 이동 -> 스케일링 -> 목표 중앙으로 이동
            Vector2 relativePos = node.Position - currentCenter;
            node.Position = targetCenter + (relativePos * finalScale);
        }


        // ★ [디버깅 로그 추가] 노드 위치가 500 안쪽으로 들어왔는지 확인
        Debug.Log($"[좌표 확인] 맵 크기: {_tileGridSize} / 첫 번째 노드 위치: {nodes[0].Position}");
        if (nodes[0].Position.x > _tileGridSize.x || nodes[0].Position.y > _tileGridSize.y)
        {
            Debug.LogError("🚨 비상! 노드가 맵 바깥에 있습니다! FitNodesToGridAsync가 실패했거나 적용되지 않았습니다.");
        }
        else
        {
            Debug.Log("✅ 노드가 맵 안으로 안전하게 이사 왔습니다.");
        }




        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 1: Noise Map Generation
    private async UniTask GenerateNoiseWorldAsync()
    {
        _noiseWorld = new float[_tileGridSize.x, _tileGridSize.y];

        float safeSeed = Mathf.Abs(_partiSettings.noiseSeed) % 10000;
        float offsetX = safeSeed * 100f;
        float offsetY = safeSeed * 100f;

        for (int x = 0; x < _tileGridSize.x; x++)
        {
            for (int y = 0; y < _tileGridSize.y; y++)
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
            for (int x = 0; x < _tileGridSize.x; x++)
                for (int y = 0; y < _tileGridSize.y; y++)
                    _territoryWorld[x, y] = -1;
            return;
        }

        HashSet<(RegionData, RegionData)> connectedRegions = new HashSet<(RegionData, RegionData)>();
        foreach (var conn in _graphResult.NodeConnections)
        {
            // 서로 다른 Region끼리 연결된 다리(Bridge)가 있다면 등록
            if (conn.ParentNode.RegionData != conn.ChildNode.RegionData)
            {
                // 양방향 모두 등록 (A->B, B->A)
                connectedRegions.Add((conn.ParentNode.RegionData, conn.ChildNode.RegionData));
                connectedRegions.Add((conn.ChildNode.RegionData, conn.ParentNode.RegionData));
            }
        }

        float mapArea = _tileGridSize.x * _tileGridSize.y;
        float avgAreaPerNode = mapArea / nodes.Count;

        float baseRadius = Mathf.Sqrt(avgAreaPerNode / Mathf.PI);
        float maxTerritoryRadius = baseRadius * 2.0f;
        float separationGap = _partiSettings.noiseStrength; // 끊어진 땅 사이 값
        if (separationGap < 2.0f) separationGap = 2.0f; // 최소값 보장

        int processedCount = 0;
        int landCount = 0;          //디버깅용 땅 카운트

        for (int x = 0; x < _tileGridSize.x; x++)
        {
            for (int y = 0; y < _tileGridSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();
                Vector2 tilePos = new Vector2(x, y);

                // 1등과 2등 노드를 모두 가져옴
                var (idx1, dist1, idx2, dist2) = FindTopTwoNodesWithNoise(tilePos, nodes);

                // 기본값: 바다(-1)
                int finalOwner = -1;

                // 조건 1: 1등 노드와의 거리가 최대 반경 이내여야 함 (섬 모양 유지)
                if (idx1 != -1 && dist1 <= maxTerritoryRadius)
                {
                    bool shouldSeparate = false;

                    // 조건 2: 2등 노드와의 경계선 근처인가?
                    if (idx2 != -1)
                    {
                        // 두 거리의 차이가 작으면 경계선 근처라는 뜻
                        float diff = dist2 - dist1;
                        if (diff < separationGap)
                        {
                            Node node1 = nodes[idx1];
                            Node node2 = nodes[idx2];

                            bool isSameRegion = (node1.RegionData == node2.RegionData);

                            if (isSameRegion)
                            {
                                shouldSeparate = false;
                            }
                            // 2. 다른 지역이면 '지역 간 연결'이 있는지 확인
                            else
                            {
                                // 캐싱해둔 정보 조회: "두 지역 사이에 다리가 하나라도 있는가?"
                                bool regionsConnected = connectedRegions.Contains((node1.RegionData, node2.RegionData));

                                // 연결이 아예 없으면 -> 찢음 (바다)
                                if (!regionsConnected)
                                {
                                    shouldSeparate = true;
                                }
                            }
                        }
                    }

                    if (!shouldSeparate)
                    {
                        finalOwner = idx1;
                        landCount++;            // 디버깅용 땅 타일 카운트
                    }
                }

                _territoryWorld[x, y] = finalOwner;

                processedCount++;
                // 배치 처리로 메인 스레드 양보
                if (processedCount % _partiSettings.batchSize == 0) await UniTask.Yield(_ct);
            }
        }
        Debug.Log($"[디버깅] 땅 타일: {landCount}개 / 바다 타일: {mapArea - landCount}개 | 최대 반경: {maxTerritoryRadius}");

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    /// <summary>
    /// 노이즈가 적용된 최근접 노드 인덱스 찾기
    /// </summary>

    private (int idx1, float dist1, int idx2, float dist2) FindTopTwoNodesWithNoise(Vector2 tilePos, List<Node> nodes)
    {
        int idx1 = -1; float dist1 = float.MaxValue;
        int idx2 = -1; float dist2 = float.MaxValue;

        int x = Mathf.Clamp((int)tilePos.x , 0, (int)_tileGridSize.x - 1);
        int y = Mathf.Clamp((int)tilePos.y, 0, (int)_tileGridSize.y - 1);
        float noiseOffset = _noiseWorld[x, y];

        for (int i = 0; i < nodes.Count; i++)
        {
            float distance = Vector2.Distance(tilePos, nodes[i].Position);

            // 이미 2등보다 훨씬 멀면 계산 스킵
            if (distance - _partiSettings.noiseStrength > dist2) continue;

            float regionNoiseFactor = Mathf.Sin(i * 0.7f) * 0.5f + 0.5f;
            float distortedDistance = distance + (noiseOffset * regionNoiseFactor);

            if (distortedDistance < dist1)
            {
                // 1등 자리를 뺏고, 기존 1등은 2등으로 밀려남
                dist2 = dist1;
                idx2 = idx1;

                dist1 = distortedDistance;
                idx1 = i;
            }
            else if (distortedDistance < dist2)
            {
                // 2등 자리만 갱신
                dist2 = distortedDistance;
                idx2 = i;
            }
        }

        return (idx1, dist1, idx2, dist2);
    }
    #endregion

    #region Phase 3: Height Map Generation

    private async UniTask GenerateHeightMapAsync()
    {
        float seedOffset = (Mathf.Abs(_partiSettings.noiseSeed) % 2000) * 50f;
        _heightWorld = new float[_tileGridSize.x, _tileGridSize.y];

        float unitHeight = _worldSettings.TileUnitHeight;
        float mapArea = _tileGridSize.x * _tileGridSize.y;

        // 노드 1개가 차지하는 평균 면적
        float avgAreaPerNode = mapArea / Mathf.Max(1, _graphResult.Nodes.Count);

        // ★ [개선 1] 구역(Region)별로 방(Node)이 몇 개 있는지 셉니다.
        Dictionary<RegionData, int> regionNodeCounts = new Dictionary<RegionData, int>();
        foreach (var node in _graphResult.Nodes)
        {
            if (node.RegionData != null)
            {
                if (!regionNodeCounts.ContainsKey(node.RegionData))
                    regionNodeCounts[node.RegionData] = 0;

                regionNodeCounts[node.RegionData]++;
            }
        }

        // 구역별 주파수 캐시
        Dictionary<RegionData, float> regionFrequencyCache = new Dictionary<RegionData, float>();

        foreach (var kvp in regionNodeCounts)
        {
            RegionData region = kvp.Key;
            int nodeCount = kvp.Value;

            // ★ [개선 2] (노드 1개 면적 * 노드 개수) = 이 구역의 실제 총 면적
            float regionArea = avgAreaPerNode * nodeCount;

            // 면적을 바탕으로 이 구역의 실제 지름(Diameter) 계산!
            float regionDiameter = Mathf.Sqrt(regionArea / Mathf.PI) * 2f;

            var noiseParams = _worldSettings.GetNoiseSettings(region.HeightNoiseTier);

            int regionHash = region.RegionName != null ? region.RegionName.GetHashCode() : region.GetInstanceID();
            float randomT = Mathf.Abs(Mathf.Sin(regionHash * 12.9898f + _partiSettings.noiseSeed)) % 1f;

            float bumps = Mathf.Lerp(noiseParams.MinBumps, noiseParams.MaxBumps, randomT);

            // 해당 구역의 '실제 지름'을 기준으로 주파수 계산
            float frequency = bumps / Mathf.Max(1f, regionDiameter); // 0으로 나누기 방지
            regionFrequencyCache[region] = frequency;
        }

        for (int x = 0; x < _tileGridSize.x; x++)
        {
            for (int y = 0; y < _tileGridSize.y; y++)
            {
                // 1. 바다 처리
                int nodeIndex = _territoryWorld[x, y];
                if (nodeIndex < 0)
                {
                    _heightWorld[x, y] = -3f;
                    continue;
                }

                // 2. 주인 노드 정보 가져오기
                Node ownerNode = _graphResult.Nodes[nodeIndex];
                RegionData currentRegion = ownerNode.RegionData;

                // 3. 펄린 노이즈 계산 (지역별 설정 사용)
                float baseHeight = _worldSettings.GetHeight(ownerNode.RegionData.BaseHeightLevel);
                var noiseParams = _worldSettings.GetNoiseSettings(ownerNode.RegionData.HeightNoiseTier);

                float frequency = regionFrequencyCache[currentRegion];

                float amplitude = noiseParams.HeightVarianceBlocks * unitHeight;

                float noiseValue = Mathf.PerlinNoise((x + seedOffset) * frequency, (y + seedOffset) * frequency);

                // 4. 최종 높이 = 기준 높이 + (노이즈 * 강도)
                float finalHeight = baseHeight + (noiseValue * amplitude);

                if (finalHeight < 0) finalHeight = 0;

                _heightWorld[x, y] = finalHeight;
            }
        }

        // 5. 스무딩 (서로 다른 노이즈 설정을 가진 방끼리 만날 때 자연스럽게 이어줌)
        await SmoothHeightWorldAsync();

        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }

    private async UniTask SmoothHeightWorldAsync()
    {

        // 스무딩된 높이 맵으로 교체
        int iterations = 3;

        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int i = 0; i < iterations; i++)
        {
            float[,] nextHeightWorld = (float[,])_heightWorld.Clone();

            int processedCount = 0;

            for (int x = 0; x < _tileGridSize.x; x++)
            {
                for (int y = 0; y < _tileGridSize.y; y++)
                {
                    // 바다는 스무딩 제외
                    if (_territoryWorld[x, y] < 0) continue;

                    float sum = _heightWorld[x, y];
                    int count = 1;

                    // 4방향 이웃의 높이를 다 더함
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d];
                        int ny = y + dy[d];

                        if (nx >= 0 && nx < _tileGridSize.x && ny >= 0 && ny < _tileGridSize.y)
                        {
                            // 이웃이 바다여도, 해안가를 부드럽게 하려면 포함 가능 (선택사항)
                            // 여기서는 육지끼리만 스무딩
                            if (_territoryWorld[nx, ny] >= 0)
                            {
                                sum += _heightWorld[nx, ny];
                                count++;
                            }
                        }
                    }

                    // 평균값 적용 (내 높이 = 이웃들과의 평균)
                    nextHeightWorld[x, y] = sum / count;

                    processedCount++;
                    if (processedCount % (_tileGridSize.x * 5) == 0) await UniTask.Yield(_ct);
                }
            }

            // 결과 갱신
            _heightWorld = nextHeightWorld;
        }
        if (_worldSettings.EnableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_worldSettings.StepDelay), cancellationToken: _ct);
    }
    #endregion

    #region Phase 4: Border and Ocean Processing
    private async UniTask ProcessBordersAsync()
    {
        int processedCount = 0;

        // 맵 가장자리를 바다/벽으로 처리
        for (int x = 0; x < _tileGridSize.x; x++)
        {
            for (int y = 0; y < _tileGridSize.y; y++)
            {
                _ct.ThrowIfCancellationRequested();

                // 가장자리 거리 계산 (0~1, 1이 중앙)
                float edgeDistanceX = Mathf.Min(x, _tileGridSize.x - 1 - x) / (float)(_tileGridSize.x * 0.5f);
                float edgeDistanceY = Mathf.Min(y, _tileGridSize.y - 1 - y) / (float)(_tileGridSize.y * 0.5f);
                float edgeDistance = Mathf.Min(edgeDistanceX, edgeDistanceY);

                // 노이즈로 해안선 불규칙하게
                float oceanNoise = _noiseWorld[x, y] / _partiSettings.noiseStrength * 0.1f;

                if (edgeDistance + oceanNoise < (1f - _partiSettings.oceanThreshold))
                {
                    _territoryWorld[x, y] = -1; // 바다/벽
                    _heightWorld[x, y] = -1; // 바다 높이
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
        _borderWorld = new int[_tileGridSize.x, _tileGridSize.y];
        System.Array.Copy(_territoryWorld, _borderWorld, _territoryWorld.Length);

        int processedCount = 0;
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int x = 1; x < _tileGridSize.x - 1; x++)
        {
            for (int y = 1; y < _tileGridSize.y - 1; y++)
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

    #region Phase 5: Assign Tiles to Regions
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
        for (int x = 0; x < _tileGridSize.x; x++)
        {
            for (int y = 0; y < _tileGridSize.y; y++)
            {
                int nodeIndex = _territoryWorld[x, y];
                if (nodeIndex >= 0 && nodeIndex < nodes.Count)
                {
                    nodes[nodeIndex].OwnedTiles.Add(new Vector2Int(x, y));
                }
                processedCount++;
                if (processedCount % (_partiSettings.batchSize * 5) == 0) 
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
        if (x < 0 || x >= _tileGridSize.x || y < 0 || y >= _tileGridSize.y)
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
        if (x < 0 || x >= _tileGridSize.x || y < 0 || y >= _tileGridSize.y)
            return false;
        return _borderWorld[x, y] == -2;
    }

    /// <summary>
    /// 특정 좌표의 높이를 반환
    /// </summary>
    public float GetHeightAt(int x, int y)
    {
        if (_heightWorld == null) return 0f;
        if (x < 0 || x >= _tileGridSize.x || y < 0 || y >= _tileGridSize.y)
            return 0f;
        return _heightWorld[x, y];
    }
    #endregion
}
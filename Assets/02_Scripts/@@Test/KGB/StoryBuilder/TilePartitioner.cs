using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// 3-4단계: 보로노이 분할 및 자연스러운 경계 처리를 담당하는 클래스
/// </summary>
public class TilePartitioner
{
    #region Settings
    [System.Serializable]
    public class PartitionSettings
    {
        [Header("Noise Settings")]
        public float noiseScale = 0.1f;          // 펄린 노이즈 스케일
        public float noiseStrength = 15f;        // 노이즈가 거리에 미치는 영향력
        public int noiseSeed = 0;                // 노이즈 시드
        
        [Header("Border Settings")]
        public int borderWidth = 2;              // 영역 경계 두께
        public float oceanThreshold = 0.85f;     // 바다로 처리할 맵 가장자리 비율
        
        [Header("Performance")]
        public int batchSize = 1000;             // 비동기 처리 시 배치 크기
    }
    #endregion

    private PartitionSettings _settings;
    private Vector2Int _worldSize;
    private int[,] _territoryWorld;    // 각 타일이 어느 region에 속하는지 (-1: 바다/벽)
    private int[,] _borderWorld;       // 경계 타일 정보 (-2: 경계)
    private float[,] _noiseWorld;      // 펄린 노이즈 캐시

    public int[,] TerritoryWorld => _territoryWorld;
    public int[,] BorderWorld => _borderWorld;

    public TilePartitioner(PartitionSettings settings = null)
    {
        _settings = settings ?? new PartitionSettings();
    }

    /// <summary>
    /// 맵 분할 메인 파이프라인
    /// </summary>
    public async UniTask PartitionWorldAsync(
        Vector2Int worldSize,
        List<TaskRegion> regions,
        CancellationToken ct)
    {
        _worldSize = worldSize;
        _territoryWorld = new int[worldSize.x, worldSize.y];
        
        // Phase 1: 노이즈 맵 생성
        GenerateNoiseWorld();
        
        // Phase 2: 보로노이 분할 (노이즈 적용)
        await AssignTerritoriesAsync(regions, ct);
        
        // Phase 3: 경계 및 바다 처리
        await ProcessBordersAsync(regions, ct);
        
        // Phase 4: 각 Region에 소유 타일 할당
        AssignOwnedTilesToRegions(regions);
    }

    #region Phase 1: Noise Map Generation
    private void GenerateNoiseWorld()
    {
        _noiseWorld = new float[_worldSize.x, _worldSize.y];
        float offsetX = _settings.noiseSeed * 100f;
        float offsetY = _settings.noiseSeed * 100f;

        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                float sampleX = (x + offsetX) * _settings.noiseScale;
                float sampleY = (y + offsetY) * _settings.noiseScale;
                
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
                
                _noiseWorld[x, y] = (noise / maxValue) * _settings.noiseStrength;
            }
        }
    }
    #endregion

    #region Phase 2: Voronoi Partitioning with Noise
    private async UniTask AssignTerritoriesAsync(List<RegionData> regions, CancellationToken ct)
    {
        if (regions == null || regions.Count == 0)
        {
            // 모든 타일을 -1 (바다/벽)로 초기화
            for (int x = 0; x < _worldSize.x; x++)
                for (int y = 0; y < _worldSize.y; y++)
                    _territoryWorld[x, y] = -1;
            return;
        }

        int processedCount = 0;

        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                ct.ThrowIfCancellationRequested();
                
                Vector2 tilePos = new Vector2(x, y);
                int closestRegionId = FindClosestRegionWithNoise(tilePos, regions);
                _territoryWorld[x, y] = closestRegionId;
                
                processedCount++;
                
                // 배치 처리로 메인 스레드 양보
                if (processedCount % _settings.batchSize == 0)
                {
                    await UniTask.Yield(ct);
                }
            }
        }
    }

    /// <summary>
    /// 노이즈가 적용된 최근접 영역 찾기
    /// </summary>
    private int FindClosestRegionWithNoise(Vector2 tilePos, List<RegionData> regions)
    {
        int closestId = -1;
        float minDistance = float.MaxValue;

        int x = Mathf.Clamp((int)tilePos.x, 0, _worldSize.x - 1);
        int y = Mathf.Clamp((int)tilePos.y, 0, _worldSize.y - 1);
        float noiseOffset = _noiseWorld[x, y];

        foreach (var region in regions)
        {
            // 기본 유클리드 거리
            float distance = Vector2.Distance(tilePos, region.center);
            
            // 조기 종료: 노이즈 최대값을 더해도 현재 최소값보다 크면 스킵
            if (distance - _settings.noiseStrength > minDistance)
                continue;
            
            // 각 영역별로 다른 노이즈 오프셋 적용 (영역 고유성)
            float regionNoiseFactor = Mathf.Sin(region.id * 0.7f) * 0.5f + 0.5f;
            float distortedDistance = distance + noiseOffset * regionNoiseFactor;
            
            if (distortedDistance < minDistance)
            {
                minDistance = distortedDistance;
                closestId = region.id;
            }
        }

        return closestId;
    }
    #endregion

    #region Phase 3: Border and Ocean Processing
    private async UniTask ProcessBordersAsync(List<RegionData> regions, CancellationToken ct)
    {
        int processedCount = 0;

        // 맵 가장자리를 바다/벽으로 처리
        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                ct.ThrowIfCancellationRequested();
                
                // 가장자리 거리 계산 (0~1, 1이 중앙)
                float edgeDistanceX = Mathf.Min(x, _worldSize.x - 1 - x) / (float)(_worldSize.x * 0.5f);
                float edgeDistanceY = Mathf.Min(y, _worldSize.y - 1 - y) / (float)(_worldSize.y * 0.5f);
                float edgeDistance = Mathf.Min(edgeDistanceX, edgeDistanceY);
                
                // 노이즈로 해안선 불규칙하게
                float oceanNoise = _noiseWorld[x, y] / _settings.noiseStrength * 0.1f;
                
                if (edgeDistance + oceanNoise < (1f - _settings.oceanThreshold))
                {
                    _territoryWorld[x, y] = -1; // 바다/벽
                }
                
                processedCount++;
                if (processedCount % _settings.batchSize == 0)
                {
                    await UniTask.Yield(ct);
                }
            }
        }

        // 영역 간 경계선 처리 (옵션)
        if (_settings.borderWidth > 0)
        {
            await MarkBorderTilesAsync(ct);
        }
    }

    /// <summary>
    /// 영역 간 경계 타일 마킹
    /// </summary>
    private async UniTask MarkBorderTilesAsync(CancellationToken ct)
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
                ct.ThrowIfCancellationRequested();
                
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
                if (processedCount % _settings.batchSize == 0)
                {
                    await UniTask.Yield(ct);
                }
            }
        }
    }
    #endregion

    #region Phase 4: Assign Tiles to Regions
    private void AssignOwnedTilesToRegions(List<TaskRegion> regions)
    {
        // 각 Region의 소유 타일 초기화
        foreach (var region in regions)
        {
            region.ownedTiles.Clear();
        }

        // Dictionary로 빠른 접근
        Dictionary<int, TaskRegion> regionLookup = new Dictionary<int, TaskRegion>();
        foreach (var region in regions)
        {
            regionLookup[region.id] = region;
        }

        // 타일 할당
        for (int x = 0; x < _worldSize.x; x++)
        {
            for (int y = 0; y < _worldSize.y; y++)
            {
                int regionId = _territoryWorld[x, y];
                if (regionId >= 0 && regionLookup.TryGetValue(regionId, out TaskRegion region))
                {
                    region.ownedTiles.Add(new Vector2Int(x, y));
                }
            }
        }
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
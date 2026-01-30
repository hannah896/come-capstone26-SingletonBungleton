using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private FDNodeGenerator _fdNodeGenerator;  // FD 노드 생성기 참조
    
    public int seed = 0;                
    public bool useRandomSeed = true;

    private MapSettings _currentSettings;
    private string _biomeLabel = "BiomeData";                                       // 바이옴 에셋 라벨  

    private List<VoronoiRegion> _regions = new();                                   // 생성된 Voronoi 영역 목록
    private List<RegionConnection> _connections = new();                            // 영역 간 연결 목록
    private List<BiomeData> _loadedBiomes = new();

    private CancellationTokenSource _cts;

    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Instance != null);
        _cts = new CancellationTokenSource();
        await LoadBiomesAsync(_cts.Token);


        // FDNodeGenerator 자동 탐색 (없으면)
        if (_fdNodeGenerator == null)
            _fdNodeGenerator = GetComponent<FDNodeGenerator>();
        

        if (_currentSettings == null)
            Debug.LogError("MapSettings가 설정되지 않았습니다!");
    }

    #region Asset Loading
    private async UniTask LoadBiomesAsync(CancellationToken ct)
    {
        try
        {
            var result = await Extensions.LoadAssetsByLabelAsync<BiomeData>(
                _biomeLabel,
                AssetCacheType.Required,
                ct
            );
            _loadedBiomes = result.OrderBy(b => b.name).ToList();
            Debug.Log($"바이옴 {_loadedBiomes.Count}개 로드 완료");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("바이옴 로드 취소됨");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"바이옴 로드 실패: {e.Message}");
        }
    }
    #endregion



    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
    #region 외부 호출 메서드
    /// <summary>
    /// 외부에서 설정을 받아 맵 생성 (유일한 진입점)
    /// </summary>
    public void GenerateMapWithSettings(MapSettings settings, bool? useFDMode = null)
    {
        _currentSettings = settings;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        GenerateMapAsync(_cts.Token).Forget();
    }


    // 맵 초기화
    private void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        _regions.Clear();
        _connections.Clear();
    }
    #endregion

 

    /// <summary>
    /// 메인 맵 생성 메서드
    /// </summary>
    public async UniTask GenerateMapAsync(CancellationToken ct = default)
    {
        if (_currentSettings == null)
        {
            Debug.LogError("MapSettings가 설정되지 않았습니다!");
            return;
        }

        if (useRandomSeed)
            seed = System.DateTime.Now.GetHashCode();
        else
            seed = Mathf.Abs(seed);
        Random.InitState(seed);

        ClearMap();

        try
        {
            // ===== 1~2단계: Forced Directed 그래프 생성====
            await GenerateGraphAsync(ct);
            
            // ===== 3~5단계: 공통 =====
            AssignBiomes();
            Debug.Log("3단계 완료: 바이옴 할당");
            await WaitStep(ct);

            ExpandBiomeRegions();
            Debug.Log("4단계 완료: 바이옴 영역 확장");
            await WaitStep(ct);

            await SpawnMapObjectsAsync(ct);
            Debug.Log("5단계 완료: 오브젝트 배치");

            Debug.Log($"맵 생성 완료: {_regions.Count}개 영역, {_connections.Count}개 연결" +
                $"\nBranch: {_currentSettings.LandBranch}," +
                $" Loop : {_currentSettings.LandLoop}");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성이 취소되었습니다.");
            ClearMap();
        }
    }

    #region 1~2단계: 그래프 생성 및 연결
    /// <summary>
    /// Force-Directed Graph 생성
    /// </summary>
    private async UniTask GenerateGraphAsync(CancellationToken ct)
    {
        // FDNodeGenerator에 설정 전달 및 실행
        _fdNodeGenerator.GenerateNodeWithSettings(_currentSettings);
        await _fdNodeGenerator.GenerateNodeAsync(ct);

        // 결과 가져오기
        var nodePositions = _fdNodeGenerator.GetNodePositions();
        var connections = _fdNodeGenerator.GetConnections();

        // Region 생성
        for (int i = 0; i < nodePositions.Count; i++)
        {
            var (pos, depth) = nodePositions[i];
            VoronoiRegion region = new VoronoiRegion
            {
                center = pos,
                id = i,
                depth = depth
            };
            _regions.Add(region);
        }
        Debug.Log($"1단계 완료 (FD): {nodePositions.Count}개 노드 생성");
        await WaitStep(ct);

        // Connection 생성
        foreach (var (fromId, toId) in connections)
        {
            if (fromId < _regions.Count && toId < _regions.Count)
            {
                var regionA = _regions[fromId];
                var regionB = _regions[toId];
                _connections.Add(new RegionConnection(regionA, regionB));
            }
        }
        
        Debug.Log($"2단계 완료 (FD): {connections.Count}개 연결 생성");
        await WaitStep(ct);
    }


    #endregion


    #region 3단계 : 바이옴 할당 
    // Voronoi 영역에 바이옴 데이터를 할당
    void AssignBiomes()
    {
        if (_loadedBiomes == null || _loadedBiomes.Count == 0) return;

        // 연결된 영역만 유효
        HashSet<VoronoiRegion> validLands = new HashSet<VoronoiRegion>();
        foreach (var conn in _connections)
        {
            validLands.Add(conn.regionA);
            validLands.Add(conn.regionB);
        }

        // Branch 설정에 따라 스폰 위치 결정
        VoronoiRegion spawnRegion = GetSpawnRegion(validLands);     //GetSpawnRegionByBranchSetting(validLands);
        spawnRegion.biome = _loadedBiomes[0]; // 첫 번째 바이옴 = 스폰 바이옴

        int maxDepth = validLands.Max(r => r.depth);

        // 펄린 노이즈 설정
        float noiseScale = 0.1f; // 더 낮춰서 큰 영역 단위로 바이옴 배치
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        foreach (var region in _regions)
        {
            if (region == spawnRegion) continue;

            if (!validLands.Contains(region))
            {
                region.biome = null;
                continue;
            }

            int availableCount = _loadedBiomes.Count - 1;
            float noiseValue = Mathf.PerlinNoise(
                    region.center.x * noiseScale + noiseOffsetX,
                    region.center.y * noiseScale + noiseOffsetY
            );  //0.0 ~ 1.0
            int biomeIndex;

            float depthRatio = (float)region.depth / maxDepth;  // 0.0 ~ 1.0
            float noiseFactor = (noiseValue - 0.5f) * 0.8f;  // -0.4 ~ +0.4

                float adjustedRatio = Mathf.Clamp01(depthRatio + noiseFactor);
                biomeIndex = Mathf.Clamp(
                1 + Mathf.FloorToInt(adjustedRatio * availableCount),
                1,
                _loadedBiomes.Count - 1
            );
            
            region.biome = _loadedBiomes[biomeIndex];
        }
    }

    /// <summary>
    /// 스폰 영역 선택 - FD 모드일 때는 depth=0 노드 우선
    /// </summary>
    VoronoiRegion GetSpawnRegion(HashSet<VoronoiRegion> validLands)
    {
        VoronoiRegion spawnRegion = validLands.FirstOrDefault(r => r.depth == 0)
            ?? validLands.First();

        return spawnRegion;
       
    }

    #endregion

    #region 4단계 : 바이옴 영역 확장 
    void ExpandBiomeRegions()
    {
        var mapSize = _currentSettings.GetMapSize();

        // 스폰 영역 식별
        VoronoiRegion spawnRegion = _regions.FirstOrDefault(r => r.biome == _loadedBiomes[0]);

        // 전체 맵의 픽셀 소유권을 추적하는 딕셔너리
        Dictionary<Vector2Int, VoronoiRegion> pixelOwnership = new();

        // 기본 반경 계산
        float baseRadius = Mathf.Sqrt((mapSize.x * mapSize.y) / (_regions.Count * 3.14f));
        float minimumGuaranteedRadius = baseRadius * 0.4f;

        // 노이즈 오프셋
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        // ===== 1. 최소 보장 영역 먼저 할당 (경쟁 없음) =====
        foreach (var region in _regions)
        {
            if (region.biome == null) continue;

            // 스폰 리전은 더 큰 최소 영역 보장
            float guaranteedRadius = (region != spawnRegion)
                ? minimumGuaranteedRadius 
                : minimumGuaranteedRadius * 1.5f;

            for (int x = (int)(region.center.x - guaranteedRadius); x <= region.center.x + guaranteedRadius; x++)
            {
                for (int y = (int)(region.center.y - guaranteedRadius); y <= region.center.y + guaranteedRadius; y++)
                {
                    if (x < 0 || x >= mapSize.x || y < 0 || y >= mapSize.y) continue;

                    Vector2 point = new Vector2(x, y);
                    float dist = Vector2.Distance(point, region.center);

                    // 최소 보장 영역 내의 픽셀은 무조건 할당
                    if (dist <= guaranteedRadius)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        
                        // 이미 다른 region의 최소 보장 영역인 경우 → 더 가까운 쪽이 가져감
                        if (pixelOwnership.TryGetValue(pos, out VoronoiRegion existingOwner))
                        {
                            float existingDist = Vector2.Distance(point, existingOwner.center);
                            if (dist < existingDist)
                            {
                                pixelOwnership[pos] = region;
                            }
                        }
                        else
                        {
                            pixelOwnership[pos] = region;
                        }
                    }
                }
            }
        }

        // ===== 2. 확장 영역 할당 (경쟁 있음, 최소 영역은 보호) =====
        Dictionary<VoronoiRegion, List<(Vector2Int pos, float priority)>> regionCandidates = new();

        foreach (var region in _regions)
        {
            if (region.biome == null) continue;

            float regionRadius = baseRadius * Random.Range(0.7f, 1.3f);
            if (region == spawnRegion)
                regionRadius = Mathf.Max(regionRadius, baseRadius * 1.2f);

            List<(Vector2Int, float)> candidates = new();

            for (int x = (int)(region.center.x - regionRadius); x <= region.center.x + regionRadius; x++)
            {
                for (int y = (int)(region.center.y - regionRadius); y <= region.center.y + regionRadius; y++)
                {
                    if (x < 0 || x >= mapSize.x || y < 0 || y >= mapSize.y) continue;

                    Vector2Int pos = new Vector2Int(x, y);
                    
                    // 이미 최소 보장 영역으로 할당된 픽셀은 건드리지 않음
                    if (pixelOwnership.ContainsKey(pos)) continue;

                    Vector2 point = new Vector2(x, y);
                    float dist = Vector2.Distance(point, region.center);

                    // 펄린 노이즈로 불규칙한 경계
                    float noiseValue = Mathf.PerlinNoise(
                        (x + noiseOffsetX) * 0.1f, 
                        (y + noiseOffsetY) * 0.1f
                    );
                    float adjustedRadius = regionRadius * (0.7f + noiseValue * 0.6f);

                    if (dist < adjustedRadius)
                    {
                        float priority = adjustedRadius - dist;
                        candidates.Add((pos, priority));
                    }
                }
            }

            regionCandidates[region] = candidates;
        }

        // 확장 영역 경쟁
        foreach (var kvp in regionCandidates)
        {
            VoronoiRegion region = kvp.Key;
            var candidates = kvp.Value;

            foreach (var (pos, priority) in candidates)
            {
                if (pixelOwnership.TryGetValue(pos, out VoronoiRegion existingOwner))
                {
                    // 최소 보장 영역은 이미 스킵했으므로, 여기는 확장 영역끼리의 경쟁
                    var existingPriority = regionCandidates[existingOwner]
                        .FirstOrDefault(c => c.pos == pos).priority;

                    if (priority > existingPriority)
                        pixelOwnership[pos] = region;
                }
                else
                {
                    pixelOwnership[pos] = region;
                }
            }
        }

        // ===== 3. 최종 할당 =====
        foreach (var kvp in pixelOwnership)
        {
            kvp.Value.points.Add(new Vector2(kvp.Key.x, kvp.Key.y));
        }
    }
    #endregion


    #region 5단계 : 맵 생성 
    async UniTask SpawnMapObjectsAsync(CancellationToken ct)
    {
        foreach (var region in _regions)
        {
            if (region.biome != null)
                await SpawnBiomeObjectsAsync(region, ct);
        }
    }

    async UniTask SpawnBiomeObjectsAsync(VoronoiRegion region, CancellationToken ct)
    {
        if (region.biome.groundPrefab != null)
        {
            Vector3 spawnPos = new Vector3(region.center.x, 0, region.center.y);
            var ground = await Extensions.SpawnAsync(region.biome.groundPrefab.name, ct);
            if (ground != null)
            {
                ground.transform.position = spawnPos;
                ground.transform.SetParent(transform);
            }
        }

        if (region.biome.essentialObjects == null)
            return;

        foreach (var obj in region.biome.essentialObjects)
        {
            if (Random.value < obj.minCount && region.points.Count > 0)
            {
                Vector2 randomPoint = region.points[Random.Range(0, region.points.Count)];
                Vector3 spawnPos = new Vector3(randomPoint.x, 0, randomPoint.y);

                var instance = await Extensions.SpawnAsync(obj.prefab.name, ct);
                await UniTask.Yield(ct);
            }
        }
    }
    #endregion


    #region Debug 
    [Header("Debug")]
    [SerializeField] private bool _enableStepByStep = false; // [New] 단계별 생성 활성화 여부
    [SerializeField] private float _stepDelay = 1.0f;        // [New] 단계별 지연 시간 (초)
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private bool _showTerritoryPoints = false;
    [SerializeField] private bool _showConnections = true;
    [SerializeField] private bool _showMapBounds = true;

    private async UniTask WaitStep(CancellationToken ct)
    {
        if (_enableStepByStep)
            await UniTask.Delay(System.TimeSpan.FromSeconds(_stepDelay), cancellationToken: ct);
        else
            await UniTask.Yield(ct);
    }

    void OnDrawGizmos()
    {
        if (!_drawGizmos || _regions.Count == 0) return;

        if (_showMapBounds)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(_currentSettings.GetMapSize().x / 2f, 0, _currentSettings.GetMapSize().y / 2f);
            Vector3 size = new Vector3(_currentSettings.GetMapSize().x, 0, _currentSettings.GetMapSize().y);
            Gizmos.DrawWireCube(center, size);
        }

        foreach (var region in _regions)
        {
            // 바이옴 할당 전에는 흰색, 할당 후에는 바이옴 색상 표시
            Gizmos.color = region.biome != null ? region.biome.debugColor : Color.white;
            Gizmos.DrawSphere(new Vector3(region.center.x, 0, region.center.y), 2f);

            if (_showTerritoryPoints)
            {
                foreach (var point in region.points)
                {
                    Gizmos.DrawCube(new Vector3(point.x, 0, point.y), new Vector3(1, 0.1f, 1));
                }
            }
        }

        Gizmos.color = Color.yellow;
        foreach (var connection in _connections)
        {
            if (_showConnections)
            {
                Vector3 start = new Vector3(connection.regionA.center.x, 0, connection.regionA.center.y);
                Vector3 end = new Vector3(connection.regionB.center.x, 0, connection.regionB.center.y);
                Gizmos.DrawLine(start, end);
            }
        }
    }
    #endregion
}

#region Data Classes
[System.Serializable]
public class VoronoiRegion
{
    public int id;
    public int depth;                       //탐색 깊이 (필요시 사용)
    public Vector2 center;                  //영역의 중심 좌표
    public List<Vector2> points = new();    //영역에 속한 픽셀 좌표 목록
    public BiomeData biome;
}

[System.Serializable]
public class RegionConnection
{
    public VoronoiRegion regionA;
    public VoronoiRegion regionB;

    public RegionConnection(VoronoiRegion a, VoronoiRegion b)
    {
        regionA = a;
        regionB = b;
    }
}


#endregion 
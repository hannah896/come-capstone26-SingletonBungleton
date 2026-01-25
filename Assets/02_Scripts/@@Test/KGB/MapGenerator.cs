using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using static UnityEngine.Rendering.RayTracingAccelerationStructure;

public class MapGenerator : MonoBehaviour
{
    private MapSettings _currentSettings;
    //TODO: [Data] 시드 관리 개선 필요
    public int seed = 0;                
    public bool useRandomSeed = true;   

    private string _biomeLabel = "BiomeData";

    private bool _drawGizmos = true;
        
    //private ResourceManager _resourceManager;
    private List<VoronoiRegion> _regions = new();                // Voronoi 영역들
    private List<RegionConnection> _connections = new();         // 영역 간 연결 정보
    private Dictionary<VoronoiRegion, List<VoronoiRegion>> _adjacencyMap = new();    // 인접 영역 맵
    private List<BiomeData> _loadedBiomes = new();               // 로드된 바이옴 데이터

    private CancellationTokenSource _cts;

    async void Start()
    {
        await UniTask.WaitUntil(() => Managers.Instance != null);

        _cts = new CancellationTokenSource();
        await LoadBiomesAsync(_cts.Token);

        if (_currentSettings != null)
        {
            await GenerateMapAsync(_cts.Token);
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async UniTask LoadBiomesAsync(CancellationToken ct)
    {
        try
        {
            var result = await Extensions.LoadAssetsByLabelAsync<BiomeData>(
                _biomeLabel,
                AssetCacheType.Required,
                ct
            );
            // 로드 완료 후 이름 순 정렬
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
            // 1단계: Voronoi 지점 생성
            GenerateVoronoiPoints();
            await UniTask.Yield(ct);

            // 2단계: 토폴로지 형성 (먼저 연결 구조 생성)
            GenerateConnections();
            await UniTask.Yield(ct);

            // 3단계: 바이옴 할당
            AssignBiomes();
            await UniTask.Yield(ct);

            // 4단계: 바이옴 영역 확장 (섬 형태로)
            ExpandBiomeRegions();
            await UniTask.Yield(ct);

            // 5단계: 맵 생성 (실제 타일/오브젝트 배치)
            await SpawnMapObjectsAsync(ct);

            Debug.Log($"맵 생성 완료: {_regions.Count}개 영역, {_connections.Count}개 연결");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("맵 생성이 취소되었습니다.");
            ClearMap();
        }
    }
    #region GenerateMapWithSettings, ClearMap
    // 외부에서 설정을 받아 맵 생성
    public void GenerateMapWithSettings(MapSettings settings)
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
        // 기존 맵 오브젝트 정리
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        _regions.Clear();
        _connections.Clear();
        _adjacencyMap.Clear();
    }
    #endregion
    #region 1단계 : Voronoi 지점 생성
    //Voronoi 지점을 랜덤하게 생성
    void GenerateVoronoiPoints()
    {
        var mapSize = _currentSettings.GetMapSize();
        int numberOfRegions = GetNumberOfRegions();

        for (int i = 0; i < numberOfRegions; i++)
        {
            Vector2 point = new Vector2(
                Random.Range(5f, mapSize.x - 5f),
                Random.Range(5f, mapSize.y - 5f)
            );

            VoronoiRegion region = new VoronoiRegion
            {
                center = point,
                id = i
            };

            _regions.Add(region);
            _adjacencyMap[region] = new List<VoronoiRegion>();
        }
    }
    #endregion

    #region 2단계 토폴로지 형성
    //2단계 : 토폴로지에 따라 연결 생성 : Voronoi 영역 간의 연결을 생성하여 맵의 토폴로지를 형성
    void GenerateConnections()
    {
        HashSet<VoronoiRegion> visited = new HashSet<VoronoiRegion>();

        // 2-1. 먼저 기본 트리 구조 생성 (모든 노드 연결)
        GenerateBranchConnections(visited);

        // 2-2. Land Branch 설정에 따라 추가 가지 생성
        AddBranchConnections(visited);

        // 2-3. Land Loop 설정에 따라 순환 경로 생성
        AddLoopConnections(visited);

        // 2-4. 인접성 맵 업데이트
        UpdateAdjacencyMap();
    }

    //2-1단계 : 기본 트리 구조 생성
    void GenerateBranchConnections(HashSet<VoronoiRegion> visited)
    {
        // 최소 신장 트리 (MST) 생성 - Prim's Algorithm
        if (_regions.Count == 0) return;

        // 맵 중앙에서 시작
        var mapSize = _currentSettings.GetMapSize();
        Vector2 mapCenter = new Vector2(mapSize.x / 2f, mapSize.y / 2f);
        VoronoiRegion start = FindClosestRegion(mapCenter);
        visited.Add(start);

        // 사용 가능한 후보 엣지 목록
        List<RegionEdge> availableEdges = new List<RegionEdge>();
        
        // 시작 지점에서 가까운 모든 영역을 후보로
        foreach (var region in _regions.Where(r => r != start))
        {
            availableEdges.Add(new RegionEdge(start, region));
        }

        // Branch 설정에 따라 연결할 노드 수 결정
        float branchFactor = _currentSettings.GetBranchMultiplier();
        int targetConnections = Mathf.Max(1, Mathf.RoundToInt(_regions.Count * (0.3f + branchFactor * 0.4f)));

        while (visited.Count < targetConnections && availableEdges.Count > 0)
        {
            // Branch 설정에 따라 선택 방식 변경
            RegionEdge selectedEdge;
            
            if (_currentSettings.LandBranchOption == LandBranchSetting.Least)
            {
                // 가장 가까운 것만 선택 (일자형)
                selectedEdge = availableEdges.OrderBy(e => Vector2.Distance(e.from.center, e.to.center)).First();
            }
            else if (_currentSettings.LandBranchOption == LandBranchSetting.Most)
            {
                // 랜덤하게 선택 (사방으로 뻗음)
                selectedEdge = availableEdges[Random.Range(0, Mathf.Min(10, availableEdges.Count))];
            }
            else
            {
                // Default: 가까운 것 중 랜덤
                var sortedEdges = availableEdges.OrderBy(e => Vector2.Distance(e.from.center, e.to.center));
                int candidateCount = Mathf.Min(4, availableEdges.Count);
                selectedEdge = sortedEdges.ElementAt(Random.Range(0, candidateCount));
            }

            availableEdges.Remove(selectedEdge);

            if (!visited.Contains(selectedEdge.to))
            {
                visited.Add(selectedEdge.to);
                _connections.Add(new RegionConnection(selectedEdge.from, selectedEdge.to));
                
                // 새로 추가된 노드에서 방문하지 않은 노드로의 엣지 추가
                foreach (var region in _regions.Where(r => !visited.Contains(r)))
                {
                    availableEdges.Add(new RegionEdge(selectedEdge.to, region));
                }
            }
        }
    }

    //2-2단계 : 추가 가지 생성
    void AddBranchConnections(HashSet<VoronoiRegion> visited)
    {
        if (_currentSettings.LandBranchOption == LandBranchSetting.Never) return;

        float branchMultiplier = _currentSettings.GetBranchMultiplier();
        if (branchMultiplier <= 0) return;

        // 연결된 노드 중에서 추가 가지 생성
        int additionalBranches = Mathf.RoundToInt(visited.Count * branchMultiplier * 0.3f);
        int added = 0;

        List<VoronoiRegion> connectedRegions = visited.ToList();
        List<VoronoiRegion> unconnectedRegions = _regions.Where(r => !visited.Contains(r)).ToList();

        for (int i = 0; i < additionalBranches && unconnectedRegions.Count > 0; i++)
        {
            VoronoiRegion from = connectedRegions[Random.Range(0, connectedRegions.Count)];
            VoronoiRegion to = unconnectedRegions[Random.Range(0, unconnectedRegions.Count)];

            _connections.Add(new RegionConnection(from, to));
            visited.Add(to);
            connectedRegions.Add(to);
            unconnectedRegions.Remove(to);
            added++;
        }
    }

    //2-3단계 : 순환 경로 생성
    void AddLoopConnections(HashSet<VoronoiRegion> visited)
    {
        if (_currentSettings.LandLoopOption == LandLoopSetting.Never) return;

        float loopMultiplier = _currentSettings.GetLoopMultiplier();
        if (loopMultiplier <= 0) return;

        // 이미 연결된 노드들 사이에 추가 연결 생성 (순환 구조)
        int additionalLoops = Mathf.RoundToInt(visited.Count * loopMultiplier * 0.2f);
        int added = 0;

        List<VoronoiRegion> connectedRegions = visited.ToList();

        for (int i = 0; i < additionalLoops && connectedRegions.Count >= 2; i++)
        {
            VoronoiRegion from = connectedRegions[Random.Range(0, connectedRegions.Count)];
            VoronoiRegion to = connectedRegions[Random.Range(0, connectedRegions.Count)];

            if (from != to && !IsConnected(from, to))
            {
                // 너무 먼 거리는 연결하지 않음
                float distance = Vector2.Distance(from.center, to.center);
                var mapSize = _currentSettings.GetMapSize();
                float maxLoopDistance = Mathf.Max(mapSize.x, mapSize.y) * 0.4f;

                if (distance < maxLoopDistance)
                {
                    _connections.Add(new RegionConnection(from, to));
                    added++;
                }
            }
        }
    }

    //2-4단계: 인접성 맵 업데이트
    void UpdateAdjacencyMap()
    {
        foreach (var conn in _connections)
        {
            if (!_adjacencyMap[conn.regionA].Contains(conn.regionB))
                _adjacencyMap[conn.regionA].Add(conn.regionB);
            
            if (!_adjacencyMap[conn.regionB].Contains(conn.regionA))
                _adjacencyMap[conn.regionB].Add(conn.regionA);
        }
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

        // 맵의 중심점 계산
        var mapSize = _currentSettings.GetMapSize();
        Vector2 geometricCenter = new Vector2(mapSize.x / 2f, mapSize.y / 2f);
        VoronoiRegion centerRegion = validLands.OrderBy(r => Vector2.Distance(r.center, geometricCenter)).First();
        centerRegion.biome = _loadedBiomes[0];

        // 펄린 노이즈 설정
        float noiseScale = 0.05f; // 더 낮춰서 큰 영역 단위로 바이옴 배치
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        foreach (var region in _regions)
        {
            if (region == centerRegion) continue;

            if (!validLands.Contains(region))
            {
                region.biome = null;
                continue;
            }

            float noiseValue = Mathf.PerlinNoise(
                region.center.x * noiseScale + noiseOffsetX,
                region.center.y * noiseScale + noiseOffsetY
            );

            if (_loadedBiomes.Count > 1)
            {
                int availableCount = _loadedBiomes.Count - 1;
                int selection = Mathf.FloorToInt(noiseValue * availableCount);
                int biomeIndex = Mathf.Clamp(selection + 1, 1, _loadedBiomes.Count - 1);
                region.biome = _loadedBiomes[biomeIndex];
            }
            else
            {
                region.biome = _loadedBiomes[0];
            }
        }
    }
    #endregion

    #region 4단계 : 바이옴 영역 확장 
    void ExpandBiomeRegions()
    {
        var mapSize = _currentSettings.GetMapSize();
        
        // 각 region을 중심으로 원형/불규칙 섬 형태로 확장
        foreach (var region in _regions)
        {
            if (region.biome == null) continue;

            // 영역 크기 결정 (랜덤하게 변화)
            float baseRadius = Mathf.Sqrt((mapSize.x * mapSize.y) / (_regions.Count * 3.14f));
            float regionRadius = baseRadius * Random.Range(0.7f, 1.3f);

            // 불규칙한 원형으로 포인트 할당
            for (int x = (int)(region.center.x - regionRadius); x <= region.center.x + regionRadius; x++)
            {
                for (int y = (int)(region.center.y - regionRadius); y <= region.center.y + regionRadius; y++)
                {
                    if (x < 0 || x >= mapSize.x || y < 0 || y >= mapSize.y) continue;

                    Vector2 point = new Vector2(x, y);
                    float dist = Vector2.Distance(point, region.center);

                    // 펄린 노이즈로 불규칙한 경계 생성
                    float noiseValue = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                    float adjustedRadius = regionRadius * (0.7f + noiseValue * 0.6f);

                    if (dist < adjustedRadius)
                    {
                        region.points.Add(point);
                    }
                }
            }
        }
    }
    #endregion

    #region Utility Methods
    VoronoiRegion FindClosestRegion(Vector2 point)
    {
        return _regions.OrderBy(r => Vector2.Distance(point, r.center)).First();
    }

    bool IsConnected(VoronoiRegion a, VoronoiRegion b)
    {
        return _connections.Any(c =>
            (c.regionA == a && c.regionB == b) ||
            (c.regionA == b && c.regionB == a));
    }

    public int GetNumberOfRegions()
    {
        Vector2Int size = _currentSettings.GetMapSize();
        int baseRegions = (size.x * size.y) / 500;
        return Mathf.RoundToInt(baseRegions * _currentSettings.DensityMultiplier);
    }
    #endregion

    #region 5단계 : 맵 생성 
    async UniTask SpawnMapObjectsAsync(CancellationToken ct)
    {
        foreach (var region in _regions)
        {
            if (region.biome != null)
            {
                await SpawnBiomeObjectsAsync(region, ct);
            }
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

        if (region.biome.spawnableObjects == null)
            return;

        foreach (var obj in region.biome.spawnableObjects)
        {
            if (Random.value < obj.spawnChance && region.points.Count > 0)
            {
                Vector2 randomPoint = region.points[Random.Range(0, region.points.Count)];
                Vector3 spawnPos = new Vector3(randomPoint.x, 0, randomPoint.y);

                var instance = await Extensions.SpawnAsync(obj.prefab.name, ct);
                await UniTask.Yield(ct);
            }
        }
    }
    #endregion

    [Header("Debug Settings")]
    public bool showRegionPixels = false;
    void OnDrawGizmos()
    {
        if (!_drawGizmos || _regions.Count == 0) return;

        foreach (var region in _regions)
        {
            Gizmos.color = region.biome != null ? region.biome.debugColor : Color.white;
            Gizmos.DrawSphere(new Vector3(region.center.x, 0, region.center.y), 1f);

            if (showRegionPixels)
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
            Vector3 start = new Vector3(connection.regionA.center.x, 0, connection.regionA.center.y);
            Vector3 end = new Vector3(connection.regionB.center.x, 0, connection.regionB.center.y);
            Gizmos.DrawLine(start, end);
        }
    }
}

#region Data Classes
[System.Serializable]
public class VoronoiRegion
{
    public int id;
    public Vector2 center;
    public List<Vector2> points = new List<Vector2>();
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

public class RegionEdge
{
    public VoronoiRegion from;
    public VoronoiRegion to;

    public RegionEdge(VoronoiRegion f, VoronoiRegion t)
    {
        from = f;
        to = t;
    }
}
#endregion
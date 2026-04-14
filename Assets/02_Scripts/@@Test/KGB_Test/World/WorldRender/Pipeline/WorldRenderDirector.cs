using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;


public interface IChunkRenderer
{
    UniTask AddChunkRenderAsync(ChunkData chunk);
    void UnloadChunk(Vector2Int coord);
}
public class WorldRenderDirector : MonoBehaviour, IChunkRenderer
{
    private TerrainBuilder _terrainBuilder;

    private TerrainLayerLoader _layerLoader;
    private TerrainPainter _terrainPainter;

    private TerrainDetailLoader _detailLoader; // ★ 추가
    private TerrainDetailPainter _detailPainter;


    //  전역 세팅 캐싱
    private WorldSettings _settings;
    private WorldGraphData _graphData;

    private TerrainLayerPalette _layerPalette;
    private TerrainDetailPalette _detailPalette; 
        

    private CancellationToken _ct;


    // 관리 중인 청크 딕셔너리 (파괴할 때 필요함)
    private Dictionary<Vector2Int, GameObject> _activeTerrains = new();
    private Dictionary<Vector2Int, TerrainData> _activeTerrainDatas = new();
    private HashSet<Vector2Int> _requestedChunks = new();

    // WorldChunkDirector가 씬 시작 시 가장 먼저 호출해 줄 초기화 함수
    public async UniTask InitializeAsync(WorldSettings settings, WorldGraphData graphData, CancellationToken ct)
    {
        _settings = settings;
        _graphData = graphData;
        _ct = ct;

        _terrainBuilder = new TerrainBuilder();
        
        _layerLoader = new TerrainLayerLoader(); // 인스턴스 생성
        _terrainPainter = new TerrainPainter();

        _detailLoader = new TerrainDetailLoader(); // 인스턴스 생성
        _detailPainter = new TerrainDetailPainter();

        // 청크 100개를 그려도 에셋 로드는 처음에 딱 1번만 하도록
        // 렌더링 파이프라인 0단계: 물감 준비
        var layerTask = _layerLoader.LoadLayersAsync(_graphData, ct);
        var detailTask = _detailLoader.LoadDetailsAsync(_graphData, ct);

        // 텍스처와 풀 이미지를 동시에 로드합니다.
        var (layerResult, detailResult) = await UniTask.WhenAll(layerTask, detailTask);

        _layerPalette = layerResult;
        _detailPalette = detailResult;

        Debug.Log("🎨 [WorldRenderDirector] 렌더링 시스템 초기화 및 에셋 로드 완료!");

        Debug.Log($"현재 등록된 텍스처 총 개수: {_layerPalette.Layers.Length}");
        foreach (var key in _layerPalette.LoadedKeys)
        {
            Debug.Log($"등록된 레이어 이름: {key} (인덱스: {_layerPalette.IndexMap[key]})");
        }
    }

    /// <summary>
    /// WorldChunkDirector가 "이 청크 그려줘!" 라고 할 때마다 호출
    /// </summary>
    public async UniTask AddChunkRenderAsync(ChunkData chunk)
    {
        if (_activeTerrains.ContainsKey(chunk.ChunkCoord) || _requestedChunks.Contains(chunk.ChunkCoord)) return;

        _requestedChunks.Add(chunk.ChunkCoord);
        Stopwatch chunkStopwatch = Stopwatch.StartNew();
        long terrainBuildMs = 0;
        long texturePaintMs = 0;
        long detailPaintMs = 0;
        long flushMs = 0;

        // 1. 지형 융기 (Terrain 생성)
        var (terrainData, terrainGO) = await _terrainBuilder.BuildChunkTerrainAsync(chunk, _settings, _ct);        // 2. 텍스처 페인팅 (로컬 데이터 기반)
        terrainBuildMs = chunkStopwatch.ElapsedMilliseconds;

        // 2. 텍스처 페인팅 (로컬 데이터 기반)
        await _terrainPainter.PaintChunkTerrainAsync(terrainData, chunk, _graphData, _layerPalette, _ct);
        texturePaintMs = chunkStopwatch.ElapsedMilliseconds - terrainBuildMs;

        // 2.5 디테일 페인팅 (육지 셀에 바이옴 DetailKeys 전부 적용)
        await _detailPainter.PaintChunkDetailsAsync(terrainData, chunk, _graphData, _detailPalette, _ct);
        detailPaintMs = chunkStopwatch.ElapsedMilliseconds - terrainBuildMs - texturePaintMs;
        terrainGO.GetComponent<Terrain>()?.Flush();
        flushMs = chunkStopwatch.ElapsedMilliseconds - terrainBuildMs - texturePaintMs - detailPaintMs;

        Debug.Log($"[WorldRenderDirector] chunk:{chunk.ChunkCoord} buildMs:{terrainBuildMs} textureMs:{texturePaintMs} detailMs:{detailPaintMs} flushMs:{flushMs} totalMs:{chunkStopwatch.ElapsedMilliseconds}");


        // 3. 오브젝트 스폰 (TODO: ObjectSpawner에게 chunk.DisposedObjects 전달하여 Instantiate)


        if (!_requestedChunks.Contains(chunk.ChunkCoord))
        {
            // 즉시 폐기 처리
            Destroy(terrainGO);
            Destroy(terrainData);
            return;
        }

        // 관리 목록에 등록하고 깔끔하게 폴더 정리
        _activeTerrains[chunk.ChunkCoord] = terrainGO;
        _activeTerrainDatas[chunk.ChunkCoord] = terrainData;
        terrainGO.transform.SetParent(this.transform);

        UpdateNeighbors(chunk.ChunkCoord);
    }

    private void UpdateNeighbors(Vector2Int coord)
    {
        if (!_activeTerrains.TryGetValue(coord, out GameObject currentGO)) return;
        Terrain current = currentGO.GetComponent<Terrain>();

        // 상하좌우 이웃 찾기
        _activeTerrains.TryGetValue(coord + Vector2Int.left, out GameObject left);
        _activeTerrains.TryGetValue(coord + Vector2Int.right, out GameObject right);
        _activeTerrains.TryGetValue(coord + Vector2Int.up, out GameObject top);
        _activeTerrains.TryGetValue(coord + Vector2Int.down, out GameObject bottom);

        // 나의 이웃 설정
        current.SetNeighbors(
            left?.GetComponent<Terrain>(),
            top?.GetComponent<Terrain>(), // 유니티는 Z+가 Top
            right?.GetComponent<Terrain>(),
            bottom?.GetComponent<Terrain>()
        );

        // 내 이웃들도 나를 이웃으로 다시 등록해야 함 (양방향 연결)
        if (left != null)
        {
            Terrain t = left.GetComponent<Terrain>();
            t.SetNeighbors(t.leftNeighbor, t.topNeighbor, current, t.bottomNeighbor);
        }
        if (right != null)
        {
            Terrain t = right.GetComponent<Terrain>();
            t.SetNeighbors(current, t.topNeighbor, t.rightNeighbor, t.bottomNeighbor);
        }
        if (top != null)
        {
            Terrain t = top.GetComponent<Terrain>();
            t.SetNeighbors(t.leftNeighbor, t.topNeighbor, t.rightNeighbor, current);
        }
        if (bottom != null)
        {
            Terrain t = bottom.GetComponent<Terrain>();
            t.SetNeighbors(t.leftNeighbor, current, t.rightNeighbor, t.bottomNeighbor);
        }
    }

    /// <summary>
    /// WorldChunkDirector가 "이 청크 멀어졌으니까 지워!" 라고 할 때 호출
    /// </summary>
    public void UnloadChunk(Vector2Int coord)
    {
        // 요청 목록에서 지움
        _requestedChunks.Remove(coord);
        if (_activeTerrains.TryGetValue(coord, out GameObject terrainGO))
        {
            Destroy(terrainGO); // 혹은 Object Pool로 반납
            _activeTerrains.Remove(coord);
        }

        if (_activeTerrainDatas.TryGetValue(coord, out TerrainData terrainData))
        {
            Destroy(terrainData); // ★ 중요: TerrainData를 파괴하지 않으면 메모리 누수 발생!
            _activeTerrainDatas.Remove(coord);
        }
    }

    private void OnDestroy()
    {
        if (_layerPalette != null && _layerPalette.LoadedKeys != null)
        {
            foreach (var key in _layerPalette.LoadedKeys)
            {
                Extensions.Release(key);
            }
            _layerPalette.LoadedKeys.Clear();
            _layerPalette.IndexMap.Clear();
            _layerPalette.Layers = null;
            _layerPalette = null;
        }
        if (_detailPalette != null && _detailPalette.LoadedKeys != null)
        {
            foreach (var key in _detailPalette.LoadedKeys)
            {
                Extensions.Release(key);
            }
            _detailPalette.LoadedKeys.Clear();
            _detailPalette.IndexMap.Clear();
            _detailPalette.Prototypes = null;
            _detailPalette = null;
        }
    }

    /// <summary>
    /// 새로운 맵을 생성하기 전, 기존에 띄워둔 모든 청크(지형)를 메모리에서 깔끔하게 날려줍니다.
    /// </summary>
    public void ClearAllChunks()
    {
        foreach (var terrainGO in _activeTerrains.Values)
        {
            if (terrainGO != null) Destroy(terrainGO);
        }
        foreach (var terrainData in _activeTerrainDatas.Values)
        {
            if (terrainData != null) Destroy(terrainData);
        }

        _activeTerrains.Clear();
        _activeTerrainDatas.Clear();
        _requestedChunks.Clear();
    }
}
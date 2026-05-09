using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class WorldChunkDirector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _loadDistance = 5;
    [SerializeField] private int _maxConcurrentChunkRenders = 4;
    [SerializeField] private Transform _trackTarget;

    private IChunkRenderer _renderDirector;
    private WorldObjectSpawner _objectSpawner;
    public WorldObjectSpawner ObjectSpawner => _objectSpawner;

    [Header("References")]
    private WorldLogicData _logicData;
    private Vector2Int _currentChunkCoord = new Vector2Int(-999, -999);

    private readonly Dictionary<Vector2Int, ChunkData> _activeChunks = new Dictionary<Vector2Int, ChunkData>();
    public IEnumerable<ChunkData> GetActiveChunks()
    {
        return _activeChunks.Values;
    }

    private bool _isUpdatingChunks;
    private bool _hasPendingRequest;
    private bool _isInitialLoadComplete = false;
    private Vector2Int _pendingCoord;
    private bool _isInitialized;


    public void Initialize(WorldLogicData logicData, IChunkRenderer renderer)
    {
        if (_renderDirector != null)
        {
            foreach (KeyValuePair<Vector2Int, ChunkData> pair in _activeChunks)
            {
                _renderDirector.UnloadChunk(pair.Key);
            }
        }

        _activeChunks.Clear();

        _logicData = logicData;
        _renderDirector = renderer;
        _currentChunkCoord = new Vector2Int(-999, -999);
        _isInitialLoadComplete = false;

        if (_objectSpawner == null)
        {
            _objectSpawner = new WorldObjectSpawner();
        }
        else
        {
            _objectSpawner.Clear();
        }

        _objectSpawner.Initialize(_logicData, transform);

        _isInitialized = (_logicData != null && _renderDirector != null);


        Main.Loop.OnUpdate -= OnChunkUpdate;
        Main.Loop.OnUpdate += OnChunkUpdate;
    }
    public void SetTarget(Transform target)
    {
        _trackTarget = target;
    }


    public async UniTask LoadInitialSpawnAreaAsync(Vector2Int spawnCoord)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning(" WorldChunkDirector가 초기화되지 않았습니다.");
            return;
        }
        Vector2Int initialCoord = ResolveInitialChunkCoord(spawnCoord);
        _currentChunkCoord = initialCoord;
        await UpdateVisibleChunks(initialCoord).AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

        _isInitialLoadComplete = true;
        Debug.Log($"[WorldChunkDirector] 초기 청크 로딩 완료: {initialCoord}");
    }

    private Vector2Int ResolveInitialChunkCoord(Vector2Int startingCoord)
    {
        if (_logicData == null)
        {
            return startingCoord;
        }

        int maxChunkX = Mathf.CeilToInt((float)_logicData.TerrainSize.x / _logicData.ChunkSize);
        int maxChunkY = Mathf.CeilToInt((float)_logicData.TerrainSize.y / _logicData.ChunkSize);

        bool likelyTileCoord = Mathf.Abs(startingCoord.x) > maxChunkX || Mathf.Abs(startingCoord.y) > maxChunkY;
        if (likelyTileCoord)
        {
            return _logicData.GetChunkCoord(startingCoord.x, startingCoord.y);
        }

        return startingCoord;
    }

    private void OnChunkUpdate(float deltaTime)
    {
        if (!_isInitialLoadComplete || _logicData == null) return;

        Transform target = _trackTarget;
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }

        if (target == null)
        {
            Debug.LogWarning("타겟이 없습니다! 업데이트 중지.");
            return;
        }

        Vector2Int playerCoord = _logicData.GetChunkCoord(
            Mathf.FloorToInt(target.position.x),
            Mathf.FloorToInt(target.position.z));

        // 플레이어 좌표가 변하고 있는지 확인
        Debug.Log($" 플레이어 월드 좌표: {target.position} / 청크 좌표: {playerCoord}");

        if (playerCoord == _currentChunkCoord) return;

        // 청크 업데이트 요청이 제대로 들어가고 있는지 확인
        Debug.Log($" 새로운 청크 로딩 요청! 이전: {_currentChunkCoord} -> 현재: {playerCoord}");

        _currentChunkCoord = playerCoord;
        RequestChunkUpdate(playerCoord).Forget();
    }

    private async UniTaskVoid RequestChunkUpdate(Vector2Int centerCoord)
    {
        if (_isUpdatingChunks)
        {
            _hasPendingRequest = true;
            _pendingCoord = centerCoord;
            return;
        }

        _isUpdatingChunks = true;

        try
        {
            Vector2Int nextCoord = centerCoord;

            while (true)
            {
                await UpdateVisibleChunks(nextCoord).AttachExternalCancellation(this.GetCancellationTokenOnDestroy()); ;

                if (!_hasPendingRequest)
                {
                    break;
                }

                _hasPendingRequest = false;
                nextCoord = _pendingCoord;
            }
        }
        finally
        {
            _isUpdatingChunks = false;
        }
    }

    private async UniTask UpdateVisibleChunks(Vector2Int centerCoord)
    {

        HashSet<Vector2Int> requiredCoords = new HashSet<Vector2Int>();

        for (int x = -_loadDistance; x <= _loadDistance; x++)
        {
            for (int y = -_loadDistance; y <= _loadDistance; y++)
            {
                requiredCoords.Add(centerCoord + new Vector2Int(x, y));
            }
        }

        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (Vector2Int coord in _activeChunks.Keys)
        {
            if (!requiredCoords.Contains(coord))
            {
                toRemove.Add(coord);
            }
        }

        foreach (Vector2Int coord in toRemove)
        {
            _activeChunks.Remove(coord);

            if (_objectSpawner != null)
            {
                _objectSpawner.DespawnChunkObjects(coord);
            }

            _renderDirector.UnloadChunk(coord);
        }

        List<Vector2Int> orderedRequiredCoords = requiredCoords
            .OrderBy(coord => (coord - centerCoord).sqrMagnitude)
            .ToList();

        List<UniTask> renderTasks = new List<UniTask>(_maxConcurrentChunkRenders);
        int concurrentLimit = Mathf.Max(1, _maxConcurrentChunkRenders);

        foreach (Vector2Int coord in orderedRequiredCoords)
        {
            if (_activeChunks.ContainsKey(coord))
            {
                continue;
            }

            ChunkData chunk = _logicData.GetOrCreateChunk(coord);
            if (chunk == null)
            {
                continue;
            }

            _activeChunks.Add(coord, chunk);
            renderTasks.Add(RenderAndSpawnChunkAsync(chunk));

            if (renderTasks.Count >= concurrentLimit)
            {
                await UniTask.WhenAll(renderTasks);
                renderTasks.Clear();
                await UniTask.Yield();
            }
        }

        if (renderTasks.Count > 0)
        {
            await UniTask.WhenAll(renderTasks);
        }
    }

    private async UniTask RenderAndSpawnChunkAsync(ChunkData chunk)
    {
        await _renderDirector.AddChunkRenderAsync(chunk);

        await _objectSpawner.SpawnChunkObjectsAsync(chunk, this.GetCancellationTokenOnDestroy());

    }

    private void OnDestroy()
    {
        if (Main.Instance != null && Main.Loop != null)
        {
            Main.Loop.OnUpdate -= OnChunkUpdate;
        }

        if (_objectSpawner != null)
        {
            _objectSpawner.Clear();
            _objectSpawner = null;
        }
    }
}
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 월드 생성 시 오브젝트를 관리하는 클래스.
/// 각 청크마다 오브젝트를 스폰하고, 청크가 언로드될 때 오브젝트를 정리.
///
/// </summary>
public class WorldObjectSpawner
{
    // 청크별로 스폰된 오브젝트 (instanceId와 함께 보관 — 파괴/원격 동기화 시 개별 조회용)
    private readonly struct SpawnedObject
    {
        public readonly int InstanceId;
        public readonly GameObject GameObject;

        public SpawnedObject(int instanceId, GameObject gameObject)
        {
            InstanceId = instanceId;
            GameObject = gameObject;
        }
    }

    private readonly Dictionary<Vector2Int, List<SpawnedObject>> _activeChunkObjects = new Dictionary<Vector2Int, List<SpawnedObject>>();
    private readonly Dictionary<Vector2Int, Transform> _activeChunkRoots = new Dictionary<Vector2Int, Transform>();

    private int _chunkSize;
    private Transform _root;

    public void Initialize(WorldLogicData logicData, Transform root)
    {
        _chunkSize = logicData != null ? logicData.ChunkSize : 0;
        _root = root;
    }

    public async UniTask SpawnChunkObjectsAsync(ChunkData chunk, CancellationToken ct)
    {
        if (chunk == null || chunk.DisposeDatas == null || chunk.DisposeDatas.Count == 0) return;
        if (_chunkSize <= 0) return;

        DespawnChunkObjects(chunk.ChunkCoord);

        Transform chunkRoot = GetOrCreateChunkRoot(chunk.ChunkCoord, _root);
        List<SpawnedObject> spawned = new List<SpawnedObject>(chunk.DisposeDatas.Count);
        _activeChunkObjects[chunk.ChunkCoord] = spawned;

        for (int i = 0; i < chunk.DisposeDatas.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            DisposeData dispose = chunk.DisposeDatas[i];
            if (dispose == null || string.IsNullOrEmpty(dispose.prefabName)) continue;
            if (chunk.IsObjectDestroyed(dispose.instanceId)) continue;

            GameObject instance = await Extensions.SpawnAsync(dispose.prefabName, chunkRoot, ct);
            if (instance == null) continue;

            // 스폰을 기다리는 사이 다른 플레이어가 부순 것으로 동기화됐다면 바로 되돌린다
            if (chunk.IsObjectDestroyed(dispose.instanceId))
            {
                Extensions.Despawn(instance);
                continue;
            }

            Vector3 position = GetDisposeWorldPosition(chunk, dispose, _chunkSize);

            instance.transform.SetPositionAndRotation(position, dispose.rotation);
            instance.transform.localScale = dispose.scale;

            InitializeDisposeComponents(instance, dispose, chunk);

            spawned.Add(new SpawnedObject(dispose.instanceId, instance));

            if ((i % 25) == 0)
            {
                await UniTask.Yield(ct);
            }
        }
    }

    /// <summary>
    /// 재생성(Respawn) 등을 위해 단일 오브젝트만 비동기로 스폰하고 청크 관리에 등록합니다.
    /// </summary>
    public async UniTask SpawnSingleObjectAsync(ChunkData chunk, DisposeData dispose)
    {
        if (dispose == null || string.IsNullOrEmpty(dispose.prefabName)) return;
        if (_chunkSize <= 0) return;

        // 1. 해당 청크의 부모 Transform 가져오기 (없으면 생성)
        Transform chunkRoot = GetOrCreateChunkRoot(chunk.ChunkCoord, _root);

        // 2. 풀매니저(Extensions)를 통해 오브젝트 스폰
        // (단일 스폰이므로 CancellationToken은 생략하거나 별도로 처리)
        GameObject instance = await Extensions.SpawnAsync(dispose.prefabName, chunkRoot);
        if (instance == null) return;

        // 3. 기존 로직을 재사용하여 정확한 높이(HeightMap)와 위치, 회전, 크기 적용
        Vector3 position = GetDisposeWorldPosition(chunk, dispose, _chunkSize);
        instance.transform.SetPositionAndRotation(position, dispose.rotation);
        instance.transform.localScale = dispose.scale;

        // 4. 초기화 인터페이스 (IDisposeInitializable) 실행
        InitializeDisposeComponents(instance, dispose, chunk);

        // 5. 🌟 핵심: 청크가 언로드될 때 같이 지워지도록 관리 리스트에 추가!
        var entry = new SpawnedObject(dispose.instanceId, instance);
        if (_activeChunkObjects.TryGetValue(chunk.ChunkCoord, out List<SpawnedObject> objects))
        {
            objects.Add(entry);
        }
        else
        {
            _activeChunkObjects[chunk.ChunkCoord] = new List<SpawnedObject> { entry };
        }
    }

    /// <summary>
    /// 현재 화면에 스폰되어 있는 배치 오브젝트를 instanceId로 찾는다.
    /// </summary>
    public bool TryGetActiveInstance(Vector2Int coord, int instanceId, out GameObject instance)
    {
        instance = null;
        if (instanceId == 0 || !_activeChunkObjects.TryGetValue(coord, out List<SpawnedObject> objects)) return false;

        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i].InstanceId != instanceId) continue;
            instance = objects[i].GameObject;
            return instance != null;
        }
        return false;
    }

    /// <summary>
    /// 배치 오브젝트를 관리 목록에서 뺀다. (오브젝트가 스스로 풀에 반납되기 직전에 호출)
    /// 빼지 않으면 청크 언로드 때 이미 반납된 오브젝트를 한 번 더 반납해,
    /// 그 사이 풀에서 재사용 중인 다른 오브젝트가 사라진다.
    /// </summary>
    public void DetachInstance(Vector2Int coord, int instanceId)
    {
        if (instanceId == 0 || !_activeChunkObjects.TryGetValue(coord, out List<SpawnedObject> objects)) return;

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i].InstanceId == instanceId)
                objects.RemoveAt(i);
        }
    }

    public void DespawnChunkObjects(Vector2Int coord)
    {
        if (_activeChunkObjects.TryGetValue(coord, out List<SpawnedObject> objects))
        {
            foreach (SpawnedObject obj in objects)
            {
                if (obj.GameObject != null) Extensions.Despawn(obj.GameObject);
            }

            _activeChunkObjects.Remove(coord);
        }

        if (_activeChunkRoots.TryGetValue(coord, out Transform root))
        {
            if (root != null) Object.Destroy(root.gameObject);
            _activeChunkRoots.Remove(coord);
        }
    }

    public void Clear()
    {
        List<Vector2Int> coords = new List<Vector2Int>(_activeChunkObjects.Keys);
        foreach (Vector2Int coord in coords)
        {
            DespawnChunkObjects(coord);
        }
    }

    private Transform GetOrCreateChunkRoot(Vector2Int coord, Transform parent)
    {
        if (_activeChunkRoots.TryGetValue(coord, out Transform root) && root != null) return root;

        GameObject rootGO = new GameObject($"Chunk_Objects_{coord.x}_{coord.y}");
        rootGO.transform.SetParent(parent, false);
        rootGO.transform.localPosition = Vector3.zero;

        root = rootGO.transform;
        _activeChunkRoots[coord] = root;
        return root;
    }

    private Vector3 GetDisposeWorldPosition(ChunkData chunk, DisposeData dispose, int chunkSize)
    {
        int localX = dispose.tilePosition.x - (chunk.ChunkCoord.x * chunkSize);
        int localY = dispose.tilePosition.y - (chunk.ChunkCoord.y * chunkSize);

        localX = Mathf.Clamp(localX, 0, chunkSize);
        localY = Mathf.Clamp(localY, 0, chunkSize);

        float height = chunk.HeightMap[localX, localY];

        return new Vector3(
            dispose.tilePosition.x + dispose.localOffset.x,
            height,
            dispose.tilePosition.y + dispose.localOffset.y);
    }

    private void InitializeDisposeComponents(GameObject instance, DisposeData dispose, ChunkData chunk)
    {
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDisposeInitializable initializable)
            {
                initializable.InitializeDispose(dispose, chunk);
            }
        }
    }
}

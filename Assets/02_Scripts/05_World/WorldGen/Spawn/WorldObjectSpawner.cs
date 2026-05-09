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
    private readonly Dictionary<Vector2Int, List<GameObject>> _activeChunkObjects = new Dictionary<Vector2Int, List<GameObject>>();
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
        if (chunk == null || chunk.PlacementDatas == null || chunk.PlacementDatas.Count == 0) return;
        if (_chunkSize <= 0) return;

        DespawnChunkObjects(chunk.ChunkCoord);

        Transform chunkRoot = GetOrCreateChunkRoot(chunk.ChunkCoord, _root);
        List<GameObject> spawned = new List<GameObject>(chunk.PlacementDatas.Count);
        _activeChunkObjects[chunk.ChunkCoord] = spawned;

        for (int i = 0; i < chunk.PlacementDatas.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            PlacementData placement = chunk.PlacementDatas[i];
            if (placement == null || string.IsNullOrEmpty(placement.prefabName)) continue;
            if (chunk.IsObjectDestroyed(placement.instanceId)) continue;

            GameObject instance = await Extensions.SpawnAsync(placement.prefabName, chunkRoot, ct);
            if (instance == null) continue;

            Vector3 position = GetPlacementWorldPosition(chunk, placement, _chunkSize);

            instance.transform.SetPositionAndRotation(position, placement.rotation);
            instance.transform.localScale = placement.scale;

            InitializePlacementComponents(instance, placement, chunk);

            spawned.Add(instance);

            if ((i % 25) == 0)
            {
                await UniTask.Yield(ct);
            }
        }
    }

    /// <summary>
    /// 재생성(Respawn) 등을 위해 단일 오브젝트만 비동기로 스폰하고 청크 관리에 등록합니다.
    /// </summary>
    public async UniTask SpawnSingleObjectAsync(ChunkData chunk, PlacementData placement)
    {
        if (placement == null || string.IsNullOrEmpty(placement.prefabName)) return;
        if (_chunkSize <= 0) return;

        // 1. 해당 청크의 부모 Transform 가져오기 (없으면 생성)
        Transform chunkRoot = GetOrCreateChunkRoot(chunk.ChunkCoord, _root);

        // 2. 풀매니저(Extensions)를 통해 오브젝트 스폰
        // (단일 스폰이므로 CancellationToken은 생략하거나 별도로 처리)
        GameObject instance = await Extensions.SpawnAsync(placement.prefabName, chunkRoot);
        if (instance == null) return;

        // 3. 기존 로직을 재사용하여 정확한 높이(HeightMap)와 위치, 회전, 크기 적용
        Vector3 position = GetPlacementWorldPosition(chunk, placement, _chunkSize);
        instance.transform.SetPositionAndRotation(position, placement.rotation);
        instance.transform.localScale = placement.scale;

        // 4. 초기화 인터페이스 (IPlacementInitializable) 실행
        InitializePlacementComponents(instance, placement, chunk);

        // 5. 🌟 핵심: 청크가 언로드될 때 같이 지워지도록 관리 리스트에 추가!
        if (_activeChunkObjects.TryGetValue(chunk.ChunkCoord, out List<GameObject> objects))
        {
            objects.Add(instance);
        }
        else
        {
            _activeChunkObjects[chunk.ChunkCoord] = new List<GameObject> { instance };
        }
    }

    public void DespawnChunkObjects(Vector2Int coord)
    {
        if (_activeChunkObjects.TryGetValue(coord, out List<GameObject> objects))
        {
            foreach (GameObject obj in objects)
            {
                if (obj != null) Extensions.Despawn(obj);
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

    private Vector3 GetPlacementWorldPosition(ChunkData chunk, PlacementData placement, int chunkSize)
    {
        int localX = placement.tilePosition.x - (chunk.ChunkCoord.x * chunkSize);
        int localY = placement.tilePosition.y - (chunk.ChunkCoord.y * chunkSize);

        localX = Mathf.Clamp(localX, 0, chunkSize);
        localY = Mathf.Clamp(localY, 0, chunkSize);

        float height = chunk.HeightMap[localX, localY];

        return new Vector3(
            placement.tilePosition.x + placement.localOffset.x,
            height,
            placement.tilePosition.y + placement.localOffset.y);
    }

    private void InitializePlacementComponents(GameObject instance, PlacementData placement, ChunkData chunk)
    {
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlacementInitializable initializable)
            {
                initializable.InitializePlacement(placement, chunk);
            }
        }
    }
}

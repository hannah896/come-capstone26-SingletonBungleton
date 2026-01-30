using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

/// <summary>
/// 오브젝트 풀링을 총괄해주는 매니저
/// </summary>
public class PoolManager : CoreManager
{
    public class PoolData
    {
        public readonly Queue<GameObject> InactiveQueue = new();
        public readonly HashSet<GameObject> AllMembers = new();
        public Transform PoolParent;
        public readonly SemaphoreSlim CreateLock = new(1, 1);
    }

    public readonly Dictionary<string, PoolData> Pools = new();
    public readonly Dictionary<GameObject, string> InstanceToAddress = new();
    private readonly Dictionary<GameObject, int> InstanceToGeneration = new();

    private GameObject _root;

    private int _generation = 0;

    private void EnsureRoot()
    {
        if (_root != null) Object.Destroy(_root);
    }

    public GameObject Spawn(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;

        EnsureRoot();
        var pool = GetOrCreatePool(address);

        var go = DequeueValidCurrentGeneration(pool);
        if (go == null)
        {
            go = Main.Data.GetData<GameObject>(address).Instantiate();
        }

        if (go.TryGetComponent<IPoolable>(out var p))
        {
            p.OnSpawn();
        }
        return go;
    }

    public T Spawn<T>(string address)
    {
        GameObject go = Spawn(address);
        if (go == null) return default;
        
        T result = go.GetComponent<T>();
        if (result == null)
        {
            Debug.LogError($"Pool \"{address}\" could not be spawned on {typeof(T)}");
            return default;
        }
        
        return result;
    }

    public async UniTask<GameObject> SpawnAsync(string address, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(address)) return null;

        EnsureRoot();
        var pool = GetOrCreatePool(address);

        var go = DequeueValidCurrentGeneration(pool);
        if (go == null)
        {
            go = Main.Data.GetData<GameObject>(address).Instantiate();
        }

        if (go.TryGetComponent<IPoolable>(out var p))
        {
            p.OnSpawn();
        }
        return go;
    }

    public async UniTask<GameObject> SpawnAsync(
        string address,
        Transform parent = null,
        CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(address)) return null;

        EnsureRoot();
        var pool = GetOrCreatePool(address);

        var go = DequeueValidCurrentGeneration(pool);
        if (go != null)
        {
            Activate(go, pool, parent);
            return go;
        }

        await pool.CreateLock.WaitAsync(token);
        try
        {
            go = DequeueValidCurrentGeneration(pool);
            if (go == null)
                go = await CreateNewInstance(address, pool, token);
        }
        finally
        {
            pool.CreateLock.Release();
        }

        if (go == null) return null;

        Activate(go, pool, parent);
        return go;
    }

    public async UniTask<T> SpawnAsync<T>(
        string address,
        Transform parent = null,
        bool worldPositionStays = false,
        bool resetLocal = true,
        CancellationToken token = default) where T : Component
    {
        var go = await SpawnAsync(address, parent, token);
        if (go == null) return null;

        if (go.TryGetComponent<T>(out var comp))
            return comp;

        Despawn(go);
        return null;
    }

    public void Despawn(GameObject go)
    {
        if (go == null) return;

        if (InstanceToGeneration.TryGetValue(go, out var gen) && gen != _generation)
        {
            SafeUnregister(go);
            Object.Destroy(go);
            return;
        }

        if (!InstanceToAddress.TryGetValue(go, out var address) || !Pools.TryGetValue(address, out var pool))
        {
            SafeUnregister(go);
            Object.Destroy(go);
            return;
        }

        if (!go.activeSelf) return;

        if (go.TryGetComponent<IPoolable>(out var p))
        {
            try { p.OnDespawn(); }
            catch { }
        }

        go.SetActive(false);
        go.transform.SetParent(pool.PoolParent, false);
        pool.InactiveQueue.Enqueue(go);
    }

    public void ClearPool(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (!Pools.TryGetValue(address, out var pool)) return;

        _generation++;

        foreach (var go in pool.AllMembers)
        {
            if (go == null) continue;
            SafeUnregister(go);
            Object.Destroy(go);
        }

        pool.AllMembers.Clear();
        pool.InactiveQueue.Clear();

        if (pool.PoolParent != null)
            Object.Destroy(pool.PoolParent.gameObject);

        Pools.Remove(address);
        Main.Resource.Release(address);
    }

    public void ClearAllPool()
    {
        _generation++;

        foreach (var key in new List<string>(Pools.Keys))
            ClearPoolInternal_NoGenBump(key); 

        Pools.Clear();
        InstanceToAddress.Clear();
        InstanceToGeneration.Clear();

        if (_root != null) Object.Destroy(_root);
        _root = null;
    }

    private void ClearPoolInternal_NoGenBump(string address)
    {
        if (!Pools.TryGetValue(address, out var pool)) return;

        foreach (var go in pool.AllMembers)
        {
            if (go == null) continue;
            SafeUnregister(go);
            Object.Destroy(go);
        }

        pool.AllMembers.Clear();
        pool.InactiveQueue.Clear();

        if (pool.PoolParent != null)
            Object.Destroy(pool.PoolParent.gameObject);

        Pools.Remove(address);
        Main.Resource.Release(address);
    }

    private void Activate(GameObject go, PoolData pool, Transform parent)
    {
        var targetParent = parent != null ? parent : pool.PoolParent;
        go.transform.SetParent(targetParent);

        go.SetActive(true);

        if (go.TryGetComponent<IPoolable>(out var p))
        {
            try { p.OnSpawn(); }
            catch { }
        }
    }

    private GameObject DequeueValidCurrentGeneration(PoolData pool)
    {
        while (pool.InactiveQueue.Count > 0)
        {
            var go = pool.InactiveQueue.Dequeue();
            if (go == null) continue;

            if (InstanceToGeneration.TryGetValue(go, out var gen) && gen == _generation)
                return go;

            SafeUnregister(go);
            Object.Destroy(go);
        }

        return null;
    }

    private async UniTask<GameObject> CreateNewInstance(string address, PoolData pool, CancellationToken token)
    {
        var prefab = await Main.Resource.LoadAssetAsync<GameObject>(address, AssetCacheType.Required, token);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, pool.PoolParent);
        go.SetActive(false);

        InstanceToAddress[go] = address;
        InstanceToGeneration[go] = _generation;
        pool.AllMembers.Add(go);

        return go;
    }

    private PoolData GetOrCreatePool(string address)
    {
        if (Pools.TryGetValue(address, out var pool))
            return pool;

        EnsureRoot();

        pool = new PoolData();
        var parentGO = new GameObject($"Pool_{address}");
        parentGO.transform.SetParent(_root.transform, false);
        pool.PoolParent = parentGO.transform;

        Pools[address] = pool;
        return pool;
    }

    private void SafeUnregister(GameObject go)
    {
        if (go == null) return;

        if (InstanceToAddress.TryGetValue(go, out var address))
        {
            InstanceToAddress.Remove(go);

            if (Pools.TryGetValue(address, out var pool))
                pool.AllMembers.Remove(go);
        }

        InstanceToGeneration.Remove(go);
    }

}
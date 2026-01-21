using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

public class PoolManager
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
    private GameObject _activeRoot;
    private GameObject _inactiveRoot;
    
    private void EnsureRoots()
    {
        if (_activeRoot == null) _activeRoot = new GameObject("@ActivePools");
        if (_inactiveRoot == null) _inactiveRoot = new GameObject("@InactivePools");
    }

    private void DestroyRoots()
    {
        if(_activeRoot != null) UnityEngine.Object.Destroy(_activeRoot);
        if(_inactiveRoot != null) UnityEngine.Object.Destroy(_inactiveRoot);
    }

    public async UniTask<GameObject> SpawnAsync(string address, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(address)) return null;

        EnsureRoots();
        var pool = GetOrCreatePool(address);

        var go = DequeueValid(pool);
        if (go != null)
        {
            Activate(go);
            return go;
        }

        await pool.CreateLock.WaitAsync(token);
        try
        {
            go = DequeueValid(pool);
            if (go == null)
                go = await CreateNewInstance(address, pool, token);
        }
        finally
        {
            pool.CreateLock.Release();
        }

        if (go == null) return null;

        Activate(go);
        return go;
    }

    public async UniTask<T> SpawnAsync<T>(string address, CancellationToken token = default) where T : Component
    {
        var go = await SpawnAsync(address, token);
        if (go == null) return null;

        if (go.TryGetComponent<T>(out var comp))
            return comp;

        Despawn(go);
        return null;
    }


    public void Despawn(GameObject go)
    {
        if (go == null) return;

        if (!InstanceToAddress.TryGetValue(go, out var address) || !Pools.TryGetValue(address, out var pool))
        {
            Object.Destroy(go);
            return;
        }

        if (!go.activeSelf) return;
        if (go.transform.parent == pool.PoolParent) return;

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

        foreach (var go in pool.AllMembers)
        {
            if (go == null) continue;
            InstanceToAddress.Remove(go);
            Object.Destroy(go);
        }

        pool.AllMembers.Clear();

        if (pool.PoolParent != null)
            Object.Destroy(pool.PoolParent.gameObject);

        Pools.Remove(address);

        Managers.Resource.Release(address);
    }

    public void ClearAllPool()
    {
        foreach (var key in new List<string>(Pools.Keys))
            ClearPool(key);

        Pools.Clear();
        InstanceToAddress.Clear();
        DestroyRoots();
    }

    private void Activate(GameObject go)
    {
        go.transform.SetParent(_activeRoot.transform, false);
        go.SetActive(true);

        if (go.TryGetComponent<IPoolable>(out var p))
        {
            try { p.OnSpawn(); }
            catch { }
        }
    }

    private GameObject DequeueValid(PoolData pool)
    {
        while (pool.InactiveQueue.Count > 0)
        {
            var go = pool.InactiveQueue.Dequeue();
            if (go != null) return go;
        }
        return null;
    }

    private async UniTask<GameObject> CreateNewInstance(string address, PoolData pool, CancellationToken token)
    {
        var prefab = await Managers.Resource.LoadAssetAsync<GameObject>(address, AssetCacheType.Required, token);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, pool.PoolParent);
        InstanceToAddress[go] = address;
        pool.AllMembers.Add(go);
        return go;
    }

    private PoolData GetOrCreatePool(string address)
    {
        if (Pools.TryGetValue(address, out var pool))
            return pool;

        EnsureRoots();

        pool = new PoolData();
        var parentGO = new GameObject($"Pool_{address}");
        parentGO.transform.SetParent(_inactiveRoot.transform, false);
        pool.PoolParent = parentGO.transform;

        Pools[address] = pool;
        return pool;
    }
}

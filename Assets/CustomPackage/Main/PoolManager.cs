using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

#region Interfaces

/// <summary>
/// 풀링 가능한 오브젝트 인터페이스.
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 풀에서 꺼내질 때 호출됩니다.
    /// </summary>
    void OnSpawn();

    /// <summary>
    /// 풀로 반환될 때 호출됩니다.
    /// </summary>
    void OnDespawn();
}

/// <summary>
/// 스폰 후 추가 초기화가 필요한 오브젝트 인터페이스.
/// </summary>
public interface IAfterSpawn
{
    /// <summary>
    /// 스폰 직후 호출됩니다.
    /// </summary>
    void AfterSpawn();
}

#endregion

/// <summary>
/// 게임 오브젝트 풀링을 관리하는 매니저.
/// 어드레서블 기반 프리팹을 로드하고 풀링하여 성능을 최적화합니다.
/// </summary>
public sealed class PoolManager : CoreManager
{
    #region Nested Classes

    // 단일 프리팹에 대한 풀 관리
    private sealed class GameObjectPool : IDisposable
    {
        #region Fields

        // 원본 프리팹
        private readonly GameObject _prefab;

        // 풀 루트 트랜스폼
        private readonly Transform _root;

        // Unity 오브젝트 풀
        private readonly IObjectPool<GameObject> _pool;

        // 풀에 속한 모든 오브젝트
        private readonly HashSet<GameObject> _allMembers = new();

        // IPoolable 컴포넌트 캐시
        private readonly Dictionary<GameObject, IPoolable> _poolableCache = new();

        // IAfterSpawn 컴포넌트 캐시
        private readonly Dictionary<GameObject, IAfterSpawn> _afterSpawnCache = new();

        // 오브젝트 파괴 시 콜백
        private readonly Action<GameObject> _onDestroyed;

        #endregion

        #region Constructor

        public GameObjectPool(string address, GameObject prefab, Transform parent, Action<GameObject> onDestroyed)
        {
            _prefab = prefab;
            _onDestroyed = onDestroyed;

            _root = new GameObject($"Pool_{address}").transform;
            _root.SetParent(parent, false);

            _pool = new ObjectPool<GameObject>(
                createFunc: CreateFunc,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyFunc,
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 1000
            );
        }

        #endregion

        #region Pool Callbacks

        // 새 인스턴스 생성
        private GameObject CreateFunc()
        {
            var inst = Object.Instantiate(_prefab, _root);
            inst.SetActive(false);

            _allMembers.Add(inst);

            var poolable = inst.GetComponent<IPoolable>();
            if (poolable != null) _poolableCache[inst] = poolable;

            var afterSpawn = inst.GetComponent<IAfterSpawn>();
            if (afterSpawn != null) _afterSpawnCache[inst] = afterSpawn;

            return inst;
        }

        // 풀에서 꺼낼 때
        private void OnGet(GameObject go)
        {
            if (go == null) return;

            go.SetActive(true);

            if (_poolableCache.TryGetValue(go, out var p))
            {
                try { p.OnSpawn(); } catch { }
            }
        }

        // 풀로 반환할 때
        private void OnRelease(GameObject go)
        {
            if (go == null) return;

            if (_poolableCache.TryGetValue(go, out var p))
            {
                try { p.OnDespawn(); } catch { }
            }

            go.SetActive(false);
            go.transform.SetParent(_root, false);
        }

        // 오브젝트 파괴 시
        private void OnDestroyFunc(GameObject go)
        {
            if (go == null) return;

            _poolableCache.Remove(go);
            _afterSpawnCache.Remove(go);
            _allMembers.Remove(go);

            _onDestroyed?.Invoke(go);

            Object.Destroy(go);
        }

        #endregion

        #region Public Methods

        // 풀에서 오브젝트 가져오기
        public GameObject Get(Transform parent)
        {
            var go = _pool.Get();
            if (go == null) return null;

            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        // AfterSpawn 콜백 실행 시도
        public bool TryAfterSpawn(GameObject go)
        {
            if (go == null) return false;

            if (_afterSpawnCache.TryGetValue(go, out var a))
            {
                try { a.AfterSpawn(); } catch { }
                return true;
            }

            return false;
        }

        // 풀로 반환
        public void Release(GameObject go)
        {
            if (go == null) return;
            _pool.Release(go);
        }

        // 풀 정리
        public void Dispose()
        {
            foreach (var go in _allMembers)
            {
                if (go == null) continue;
                _onDestroyed?.Invoke(go);
                Object.Destroy(go);
            }

            _allMembers.Clear();
            _poolableCache.Clear();
            _afterSpawnCache.Clear();

            if (_root != null) Object.Destroy(_root.gameObject);
        }

        #endregion
    }

    #endregion

    #region Fields

    // 주소별 풀 캐시
    private readonly Dictionary<string, GameObjectPool> _pools = new();

    // 인스턴스 → 풀 매핑
    private readonly Dictionary<GameObject, GameObjectPool> _instanceToPool = new();

    // 로딩 중인 풀 태스크
    private readonly Dictionary<string, UniTask<GameObjectPool>> _loadingTasks = new();

    // 풀 루트 오브젝트
    private GameObject _root;

    #endregion

    #region Spawn

    /// <summary>
    /// 풀에서 게임 오브젝트를 스폰합니다.
    /// </summary>
    public async UniTask<GameObject> SpawnAsync(string address, Transform parent = null, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(address)) return null;

        var pool = _pools.TryGetValue(address, out var cached) ? cached : await GetOrLoadPool(address, token);
        if (pool == null) return null;

        await UniTask.SwitchToMainThread(token);

        var go = pool.Get(parent);
        if (go == null) return null;

        _instanceToPool[go] = pool;

        pool.TryAfterSpawn(go);

        return go;
    }

    /// <summary>
    /// 풀에서 특정 컴포넌트가 있는 게임 오브젝트를 스폰합니다.
    /// </summary>
    public async UniTask<T> SpawnAsync<T>(string address, Transform parent = null, CancellationToken token = default) where T : Component
    {
        var go = await SpawnAsync(address, parent, token);
        if (go == null) return null;

        if (go.TryGetComponent<T>(out var comp)) return comp;
        return go.GetComponent<T>();
    }

    #endregion

    #region Despawn

    /// <summary>
    /// 게임 오브젝트를 풀로 반환합니다.
    /// </summary>
    public void Despawn(GameObject go)
    {
        if (go == null) return;

        if (!PlayerLoopHelper.IsMainThread)
        {
            DespawnAsync(go).Forget();
            return;
        }

        DespawnInternal(go);
    }

    // 비동기 디스폰 처리
    private async UniTaskVoid DespawnAsync(GameObject go)
    {
        await UniTask.SwitchToMainThread();
        DespawnInternal(go);
    }

    // 디스폰 내부 로직
    private void DespawnInternal(GameObject go)
    {
        if (go == null) return;

        if (_instanceToPool.TryGetValue(go, out var pool) && pool != null)
        {
            pool.Release(go);
            return;
        }

        Object.Destroy(go);
    }

    #endregion

    #region Pool Management

    // 풀 로드 또는 가져오기
    private async UniTask<GameObjectPool> GetOrLoadPool(string address, CancellationToken token)
    {
        if (_pools.TryGetValue(address, out var existing)) return existing;

        if (_loadingTasks.TryGetValue(address, out var loadingTask))
            return await loadingTask;

        var task = LoadInternal(address, token);
        _loadingTasks[address] = task;

        try
        {
            var pool = await task;

            if (_root == null)
            {
                pool?.Dispose();
                return null;
            }

            if (pool != null) _pools[address] = pool;
            return pool;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
        finally
        {
            _loadingTasks.Remove(address);
        }
    }

    // 프리팹 로드 및 풀 생성
    private async UniTask<GameObjectPool> LoadInternal(string address, CancellationToken token)
    {
        GameObject prefab = null;

        try
        {
            prefab = await Main.Resource.LoadAssetAsync<GameObject>(address, ct: token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }

        if (prefab == null) return null;

        await UniTask.SwitchToMainThread(token);

        EnsureRoot();

        return new GameObjectPool(address, prefab, _root.transform, go => _instanceToPool.Remove(go));
    }

    // 루트 오브젝트 생성 보장
    private void EnsureRoot()
    {
        if (_root == null) _root = new GameObject("@Pool_Root");
    }

    /// <summary>
    /// 특정 주소의 풀을 정리합니다.
    /// </summary>
    public void ClearPool(string address)
    {
        if (string.IsNullOrEmpty(address)) return;

        if (!PlayerLoopHelper.IsMainThread)
        {
            ClearPoolAsync(address).Forget();
            return;
        }

        if (_pools.TryGetValue(address, out var pool))
        {
            pool.Dispose();
            _pools.Remove(address);
            Main.Resource.Release(address);
        }
    }

    // 비동기 풀 정리
    private async UniTaskVoid ClearPoolAsync(string address)
    {
        await UniTask.SwitchToMainThread();
        ClearPool(address);
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        if (!PlayerLoopHelper.IsMainThread)
        {
            ClearAsync().Forget();
            return;
        }

        foreach (var pool in _pools.Values)
        {
            pool.Dispose();
        }

        _pools.Clear();
        _instanceToPool.Clear();
        _loadingTasks.Clear();

        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }
    }

    // 비동기 정리
    private async UniTaskVoid ClearAsync()
    {
        await UniTask.SwitchToMainThread();
        Clear();
    }

    #endregion
}

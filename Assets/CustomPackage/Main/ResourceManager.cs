using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;


/// <summary>
/// 어드레서블로 데이터를 불러와 캐싱해두는 매니저
/// </summary>
public class ResourceManager : CoreManager
{
    private const string TAG = "[AddressableManager]";

    private readonly Dictionary<string, AsyncOperationHandle> _requiredCache = new();               // 계속 쓸 데이터
    private readonly Dictionary<string, AsyncOperationHandle> _nonRequiredCache = new();            // 일시적인 데이터
    private readonly Dictionary<string, AsyncOperationHandle> _loadingRequiredCache = new();        // 로딩중인 계속 쓸 데이터
    private readonly Dictionary<string, AsyncOperationHandle> _loadingNonRequiredCache = new();     // 로딩중인 일시적인 데이터

    private readonly Dictionary<string, List<string>> _labelToKeys = new();
    private readonly HashSet<string> _logged = new();

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        var handle = Addressables.InitializeAsync();
        await handle.ToUniTask();
    }

    private Dictionary<string, AsyncOperationHandle> GetCache(AssetCacheType cacheType)
        => cacheType == AssetCacheType.Required ? _requiredCache : _nonRequiredCache;

    private Dictionary<string, AsyncOperationHandle> GetLoadingCache(AssetCacheType cacheType)
        => cacheType == AssetCacheType.Required ? _loadingRequiredCache : _loadingNonRequiredCache;

    private bool TryGetValidHandle(string key, out AsyncOperationHandle handle)
    {
        if (_requiredCache.TryGetValue(key, out handle) && handle.IsValid()) return true;
        if (_nonRequiredCache.TryGetValue(key, out handle) && handle.IsValid()) return true;
        return false;
    }

    public async UniTask<AsyncOperationHandle> LoadHandleAsync(
        string key = null,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(key)) return default;

        if (TryGetValidHandle(key, out var cachedHandle)) return cachedHandle;

        var loadingCache = GetLoadingCache(cacheType);
        if (loadingCache.TryGetValue(key, out var hLoading) && hLoading.IsValid())
        {
            await hLoading.ToUniTask(cancellationToken: ct);
            return hLoading;
        }

        var handle = Addressables.LoadAssetAsync<object>(key);
        loadingCache[key] = handle;

        try
        {
            await handle.ToUniTask(cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            loadingCache.Remove(key);
            throw;
        }
        catch (Exception e)
        {
            LogOnce($"handle_fail|{key}", $"{TAG} 핸들 로드 실패 key='{key}': {e.Message}");
            if (handle.IsValid()) Addressables.Release(handle);
            loadingCache.Remove(key);
            return default;
        }

        loadingCache.Remove(key);

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            return default;
        }

        GetCache(cacheType)[key] = handle;
        return handle;
    }

    public async UniTask<T> LoadAssetAsync<T>(
        string key = null,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken ct = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;

        var handle = await LoadHandleAsync(key, cacheType, ct);
        if (!handle.IsValid()) return null;

        if (handle.Result is T result) return result;

        if (typeof(Component).IsAssignableFrom(typeof(T)))
        {
            if (handle.Result is GameObject go)
            {
                T component = go.GetComponent<T>();
                if (component != null) return component;
            }
        }

        LogOnce($"type_mismatch|{key}", $"{TAG} 타입 불일치: {key} (결과:{handle.Result.GetType().Name}) is not {typeof(T).Name}");
        return null;
    }

    public T GetAssetNow<T>(string key) where T : UnityEngine.Object
    {
        if (TryGetValidHandle(key, out var handle) && handle.Result is T result)
        {
            return result;
        }
        return null;
    }

    public async UniTask<List<T>> LoadAssetsByLabelAsync<T>(
        string label,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken ct = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(label)) return new List<T>();

        var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
        IList<IResourceLocation> locations;
        try
        {
            locations = await locHandle.ToUniTask(cancellationToken: ct);
        }
        finally { if (locHandle.IsValid()) Addressables.Release(locHandle); }

        if (locations == null || locations.Count == 0) return new List<T>();

        if (!_labelToKeys.ContainsKey(label)) _labelToKeys[label] = new List<string>();

        var tasks = new List<UniTask<T>>(locations.Count);
        foreach (var loc in locations)
        {
            if (!_labelToKeys[label].Contains(loc.PrimaryKey))
                _labelToKeys[label].Add(loc.PrimaryKey);

            tasks.Add(LoadAssetAsync<T>(loc.PrimaryKey, cacheType, ct));
        }

        var loaded = await UniTask.WhenAll(tasks);
        var results = new List<T>();
        foreach (var item in loaded) if (item != null) results.Add(item);

        return results;
    }

    public void Release(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (ReleaseFromCache(_nonRequiredCache, key)) return;
        ReleaseFromCache(_requiredCache, key);
    }

    public void ReleaseLabel(string label)
    {
        if (string.IsNullOrEmpty(label) || !_labelToKeys.TryGetValue(label, out var keys))
            return;

        foreach (var key in keys) Release(key);
        _labelToKeys.Remove(label);
    }

    public void ClearNonRequired()
    {
        ReleaseAllInDictionary(_nonRequiredCache);
        ReleaseAllInDictionary(_loadingNonRequiredCache);
        CleanupLabelCache();
    }

    public void ClearRequired()
    {
        ReleaseAllInDictionary(_requiredCache);
        ReleaseAllInDictionary(_loadingRequiredCache);
        _labelToKeys.Clear();
    }

    public void ClearAll()
    {
        ClearNonRequired();
        ClearRequired();
        _logged.Clear();
    }

    private bool ReleaseFromCache(Dictionary<string, AsyncOperationHandle> cache, string key)
    {
        if (!cache.TryGetValue(key, out var handle)) return false;
        if (handle.IsValid()) Addressables.Release(handle);
        cache.Remove(key);
        return true;
    }

    private void ReleaseAllInDictionary(Dictionary<string, AsyncOperationHandle> dict)
    {
        foreach (var h in dict.Values) if (h.IsValid()) Addressables.Release(h);
        dict.Clear();
    }

    private void CleanupLabelCache()
    {
        foreach (var label in new List<string>(_labelToKeys.Keys))
        {
            _labelToKeys[label].RemoveAll(key =>
                !_requiredCache.ContainsKey(key) && !_nonRequiredCache.ContainsKey(key));

            if (_labelToKeys[label].Count == 0) _labelToKeys.Remove(label);
        }
    }

    private void LogOnce(string signature, string message)
    {
        if (_logged.Add(signature)) Debug.LogWarning(message);
    }
}
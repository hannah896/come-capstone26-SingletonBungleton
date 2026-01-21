using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class ResourceManager
{
    private const string TAG = "[AddressableManager]";

    private readonly Dictionary<string, AsyncOperationHandle> _requiredCache = new();
    private readonly Dictionary<string, AsyncOperationHandle> _nonRequiredCache = new();
    private readonly Dictionary<string, AsyncOperationHandle> _loadingRequiredCache = new();
    private readonly Dictionary<string, AsyncOperationHandle> _loadingNonRequiredCache = new();

    private readonly Dictionary<string, List<string>> _labelToKeys = new();

    private readonly HashSet<string> _logged = new();

    public async UniTask Init(CancellationToken cancellationToken = default)
    {
        try
        {
            var handle = Addressables.InitializeAsync();
            await handle.ToUniTask(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            LogOnce($"init|{e.GetType().Name}", $"{TAG} Initialize 실패: {e.GetType().Name} - {e.Message}");
            throw;
        }
    }

    #region Loading Logic
    private Dictionary<string, AsyncOperationHandle> GetCache(AssetCacheType cacheType)
        => cacheType == AssetCacheType.Required ? _requiredCache : _nonRequiredCache;

    private Dictionary<string, AsyncOperationHandle> GetLoadingCache(AssetCacheType cacheType)
        => cacheType == AssetCacheType.Required ? _loadingRequiredCache : _loadingNonRequiredCache;

    public async UniTask<T> LoadAssetAsync<T>(
        string key,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken cancellationToken = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (TryGetValidCachedResult(_requiredCache, key, out T r0)) return r0;
        if (TryGetValidCachedResult(_nonRequiredCache, key, out T r1)) return r1;

        var lr = await AwaitLoadingHandleAs<T>(_loadingRequiredCache, key, cancellationToken);
        if (lr != null) return lr;
        var lnr = await AwaitLoadingHandleAs<T>(_loadingNonRequiredCache, key, cancellationToken);
        if (lnr != null) return lnr;

        var loadingTarget = GetLoadingCache(cacheType);
        var handle = Addressables.LoadAssetAsync<T>(key);
        loadingTarget[key] = handle;

        try
        {
            await handle.ToUniTask(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            loadingTarget.Remove(key);
            throw;
        }
        catch (Exception e)
        {
            LogOnce($"asset_fail|{key}|{typeof(T).Name}", $"{TAG} 로드 실패 key='{key}': {e.Message}");
            if (handle.IsValid()) Addressables.Release(handle);
            loadingTarget.Remove(key);
            return null;
        }

        loadingTarget.Remove(key);

        if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }

        GetCache(cacheType)[key] = handle;
        return handle.Result as T;
    }

    public async UniTask<List<T>> LoadAssetsByLabelAsync<T>(
        string label,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken cancellationToken = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(label)) return new List<T>();

        var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
        IList<IResourceLocation> locations;
        try
        {
            locations = await locHandle.ToUniTask(cancellationToken: cancellationToken);
        }
        finally { if (locHandle.IsValid()) Addressables.Release(locHandle); }

        if (locations == null || locations.Count == 0) return new List<T>();

        if (!_labelToKeys.ContainsKey(label)) _labelToKeys[label] = new List<string>();

        var tasks = new List<UniTask<T>>(locations.Count);
        foreach (var loc in locations)
        {
            if (!_labelToKeys[label].Contains(loc.PrimaryKey))
                _labelToKeys[label].Add(loc.PrimaryKey);

            tasks.Add(LoadAssetAsync<T>(loc.PrimaryKey, cacheType, cancellationToken));
        }

        var loaded = await UniTask.WhenAll(tasks);
        var results = new List<T>();
        foreach (var item in loaded) if (item != null) results.Add(item);

        return results;
    }
    #endregion

    #region Release Logic
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

        foreach (var key in keys)
        {
            Release(key);
        }

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
    #endregion

    #region Utils
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

    private bool TryGetValidCachedResult<T>(Dictionary<string, AsyncOperationHandle> cache, string key, out T result) where T : UnityEngine.Object
    {
        result = null;
        if (cache.TryGetValue(key, out var h) && h.IsValid() && h.Status == AsyncOperationStatus.Succeeded)
        {
            result = h.Result as T;
            return result != null;
        }
        return false;
    }

    private async UniTask<T> AwaitLoadingHandleAs<T>(Dictionary<string, AsyncOperationHandle> loadingCache, string key, CancellationToken ct) where T : UnityEngine.Object
    {
        if (!loadingCache.TryGetValue(key, out var h) || !h.IsValid()) return null;
        try { await h.ToUniTask(cancellationToken: ct); } catch { return null; }
        return h.Result as T;
    }
    #endregion
}
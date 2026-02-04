using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

#region Enums

/// <summary>
/// 에셋 캐시 타입.
/// </summary>
public enum AssetCacheType
{
    /// <summary>
    /// 필수 에셋. 명시적으로 해제하기 전까지 유지됩니다.
    /// </summary>
    Required,

    /// <summary>
    /// 비필수 에셋. 씬 변경 시 해제될 수 있습니다.
    /// </summary>
    NonRequired
}

#endregion

/// <summary>
/// 어드레서블로 데이터를 불러와 캐싱해두는 매니저.
/// Required/NonRequired 두 가지 캐시 레벨을 지원합니다.
/// </summary>
public class ResourceManager : PrimaryManager
{
    #region Constants

    private const string Tag = "[ResourceManager]";

    #endregion

    #region Fields

    // Required 캐시 (계속 유지)
    private readonly Dictionary<string, AsyncOperationHandle> _requiredCache = new();

    // NonRequired 캐시 (씬 별 유지)
    private readonly Dictionary<string, AsyncOperationHandle> _nonRequiredCache = new();

    // 로딩 중인 핸들
    private readonly Dictionary<string, AsyncOperationHandle> _loadingCache = new();

    // 로딩 중 캐시타입이 다르게 들어올 경우 승급을 위한 딕셔너리
    private readonly Dictionary<string, AssetCacheType> _desiredType = new();

    // 라벨 단위 릴리즈를 위한 딕셔너리
    private readonly Dictionary<string, List<string>> _labelToKeys = new();

    // 중복 로그 방지용 집합
    private readonly HashSet<string> _logged = new();

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await Addressables.InitializeAsync().ToUniTask();
    }

    #endregion

    #region Load Asset

    /// <summary>
    /// 에셋 핸들을 비동기로 로드합니다.
    /// </summary>
    public async UniTask<AsyncOperationHandle> LoadHandleAsync(
        string key,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(key)) return default;

        MarkDesiredType(key, cacheType);

        if (TryGetValidHandle(key, out var cachedHandle)) return cachedHandle;

        if (_loadingCache.TryGetValue(key, out var hLoading))
        {
            if (hLoading.IsValid())
            {
                await hLoading.ToUniTask(cancellationToken: ct);

                if (_desiredType.TryGetValue(key, out var want) && want == AssetCacheType.Required)
                    PromoteToRequiredIfNeeded(key);

                if (TryGetValidHandle(key, out var finishedHandle)) return finishedHandle;
                return default;
            }
            _loadingCache.Remove(key);
        }

        var handle = Addressables.LoadAssetAsync<object>(key);
        _loadingCache[key] = handle;

        try
        {
            await handle.ToUniTask(cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            _loadingCache.Remove(key);
            CleanupDesiredType();
            throw;
        }
        catch (Exception e)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            _loadingCache.Remove(key);
            LogOnce($"handle_fail|{key}", $"{Tag} Load Fail key='{key}': {e.Message}");
            CleanupDesiredType();
            return default;
        }

        _loadingCache.Remove(key);

        if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            CleanupDesiredType();
            return default;
        }

        var finalType = _desiredType.GetValueOrDefault(key, cacheType);

        if (finalType == AssetCacheType.Required)
            PromoteToRequiredIfNeeded(key);

        if (TryGetValidHandle(key, out var alreadyCached))
        {
            if (handle.IsValid() && !alreadyCached.Equals(handle))
                Addressables.Release(handle);

            CleanupDesiredType();
            return alreadyCached;
        }

        var cache = GetCache(finalType);
        if (cache.TryGetValue(key, out var existingInSameCache) && existingInSameCache.IsValid())
        {
            if (handle.IsValid() && !existingInSameCache.Equals(handle))
                Addressables.Release(handle);

            CleanupDesiredType();
            return existingInSameCache;
        }

        cache[key] = handle;

        CleanupDesiredType();
        return handle;
    }

    /// <summary>
    /// 에셋을 비동기로 로드합니다.
    /// </summary>
    public async UniTask<T> LoadAssetAsync<T>(
        string key = null,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken ct = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;

        var handle = await LoadHandleAsync(key, cacheType, ct);
        if (!handle.IsValid()) return null;

        if (handle.Result is T result) return result;

        if (typeof(Component).IsAssignableFrom(typeof(T)) && handle.Result is GameObject go)
        {
            if (go.TryGetComponent<T>(out var component)) return component;
        }

        LogOnce($"type_mismatch|{key}",
            $"{Tag} Type Mismatch: Key='{key}' Expected='{typeof(T).Name}' Actual='{handle.Result?.GetType().Name}'");
        return null;
    }

    /// <summary>
    /// 캐시된 에셋을 동기적으로 가져옵니다.
    /// </summary>
    public T GetAssetNow<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (TryGetValidHandle(key, out var handle) && handle.Result is T result) return result;
        return null;
    }

    /// <summary>
    /// 라벨로 여러 에셋을 비동기로 로드합니다.
    /// </summary>
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new List<T>();
        }
        finally
        {
            if (locHandle.IsValid()) Addressables.Release(locHandle);
        }

        if (locations == null || locations.Count == 0) return new List<T>();

        if (!_labelToKeys.TryGetValue(label, out var groupKeys))
        {
            groupKeys = new List<string>();
            _labelToKeys[label] = groupKeys;
        }

        var tasks = new List<UniTask<T>>(locations.Count);
        foreach (var loc in locations)
        {
            if (!groupKeys.Contains(loc.PrimaryKey))
                groupKeys.Add(loc.PrimaryKey);

            tasks.Add(LoadAssetAsync<T>(loc.PrimaryKey, cacheType, ct));
        }

        var results = await UniTask.WhenAll(tasks);

        var valid = new List<T>(results.Length);
        foreach (var item in results)
        {
            if (item != null) valid.Add(item);
        }

        return valid;
    }

    #endregion

    #region Release

    /// <summary>
    /// 특정 키의 에셋을 해제합니다.
    /// </summary>
    public void Release(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (ReleaseFromCache(_nonRequiredCache, key)) { _desiredType.Remove(key); return; }
        if (ReleaseFromCache(_requiredCache, key)) { _desiredType.Remove(key); return; }

        _desiredType.Remove(key);
        _loadingCache.Remove(key);
    }

    /// <summary>
    /// 라벨에 속한 모든 에셋을 해제합니다.
    /// </summary>
    public void ReleaseLabel(string label)
    {
        if (string.IsNullOrEmpty(label) || !_labelToKeys.TryGetValue(label, out var keys))
            return;

        var snapshot = new List<string>(keys);
        foreach (var key in snapshot) Release(key);
        _labelToKeys.Remove(label);

        CleanupDesiredType();
    }

    /// <summary>
    /// NonRequired 캐시를 모두 해제합니다.
    /// </summary>
    public void ClearNonRequired()
    {
        ReleaseAllInDictionary(_nonRequiredCache);
        CleanupLabelCache();
        CleanupDesiredType();
    }

    /// <summary>
    /// Required 캐시를 모두 해제합니다.
    /// </summary>
    public void ClearRequired()
    {
        ReleaseAllInDictionary(_requiredCache);
        _labelToKeys.Clear();
        CleanupDesiredType();
    }

    /// <summary>
    /// 모든 캐시를 해제합니다.
    /// </summary>
    public void ClearAll()
    {
        ClearNonRequired();
        ClearRequired();
        ClearLoading();
        _desiredType.Clear();
        _logged.Clear();
    }

    public override void Clear()
    {
        ClearNonRequired();
        _logged.Clear();
    }

    #endregion

    #region Internal Methods

    // 캐시 타입에 해당하는 딕셔너리 반환
    private Dictionary<string, AsyncOperationHandle> GetCache(AssetCacheType cacheType)
        => cacheType == AssetCacheType.Required ? _requiredCache : _nonRequiredCache;

    // 두 캐시 타입 중 우선순위가 높은 것 반환
    private static AssetCacheType MergeType(AssetCacheType a, AssetCacheType b)
        => (a == AssetCacheType.Required || b == AssetCacheType.Required)
            ? AssetCacheType.Required
            : AssetCacheType.NonRequired;

    // 희망 캐시 타입 기록
    private void MarkDesiredType(string key, AssetCacheType requested)
    {
        if (_desiredType.TryGetValue(key, out var prev))
            _desiredType[key] = MergeType(prev, requested);
        else
            _desiredType[key] = requested;

        if (_desiredType[key] == AssetCacheType.Required)
            PromoteToRequiredIfNeeded(key);
    }

    // 유효한 핸들 조회 시도
    private bool TryGetValidHandle(string key, out AsyncOperationHandle handle)
    {
        if (_requiredCache.TryGetValue(key, out handle))
        {
            if (handle.IsValid()) return true;
            _requiredCache.Remove(key);
        }

        if (_nonRequiredCache.TryGetValue(key, out handle))
        {
            if (handle.IsValid()) return true;
            _nonRequiredCache.Remove(key);
        }

        handle = default;
        return false;
    }

    // NonRequired에서 Required로 승급
    private void PromoteToRequiredIfNeeded(string key)
    {
        if (_nonRequiredCache.TryGetValue(key, out var h))
        {
            if (h.IsValid())
            {
                _nonRequiredCache.Remove(key);
                _requiredCache[key] = h;
            }
            else
            {
                _nonRequiredCache.Remove(key);
            }
        }
    }

    // 캐시에서 핸들 해제
    private bool ReleaseFromCache(Dictionary<string, AsyncOperationHandle> cache, string key)
    {
        if (!cache.TryGetValue(key, out var handle)) return false;
        if (handle.IsValid()) Addressables.Release(handle);
        cache.Remove(key);
        return true;
    }

    // 딕셔너리의 모든 핸들 해제
    private void ReleaseAllInDictionary(Dictionary<string, AsyncOperationHandle> dict)
    {
        var handles = new List<AsyncOperationHandle>(dict.Values);
        dict.Clear();

        foreach (var h in handles)
        {
            if (h.IsValid()) Addressables.Release(h);
        }
    }

    // 로딩 중인 핸들 모두 정리
    private void ClearLoading()
    {
        var handles = new List<AsyncOperationHandle>(_loadingCache.Values);
        _loadingCache.Clear();

        foreach (var h in handles)
        {
            if (h.IsValid()) Addressables.Release(h);
        }

        CleanupDesiredType();
    }

    // 라벨 캐시 정리
    private void CleanupLabelCache()
    {
        var labels = new List<string>(_labelToKeys.Keys);
        foreach (var label in labels)
        {
            var keys = _labelToKeys[label];
            keys.RemoveAll(key => !_requiredCache.ContainsKey(key) && !_nonRequiredCache.ContainsKey(key));
            if (keys.Count == 0) _labelToKeys.Remove(label);
        }
    }

    // 희망 타입 딕셔너리 정리
    private void CleanupDesiredType()
    {
        if (_desiredType.Count == 0) return;

        var keys = new List<string>(_desiredType.Keys);
        foreach (var key in keys)
        {
            if (!_requiredCache.ContainsKey(key) &&
                !_nonRequiredCache.ContainsKey(key) &&
                !_loadingCache.ContainsKey(key))
            {
                _desiredType.Remove(key);
            }
        }
    }

    // 중복 방지 로그 출력
    private void LogOnce(string signature, string message)
    {
        if (_logged.Add(signature)) Debug.LogWarning(message);
    }

    #endregion
}

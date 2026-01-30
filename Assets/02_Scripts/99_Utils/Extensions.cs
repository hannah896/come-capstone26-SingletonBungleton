using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public static class Extensions
{
    #region Resource
    public static async UniTask<T> LoadAssetAsync<T>(string key, AssetCacheType cacheType = AssetCacheType.NonRequired, CancellationToken token = default) where T : UnityEngine.Object
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Resource.LoadAssetAsync<T>(key, cacheType, cts.Token);
    }

    public static async UniTask<List<T>> LoadAssetsByLabelAsync<T>(string label, AssetCacheType cacheType = AssetCacheType.NonRequired, CancellationToken token = default) where T : UnityEngine.Object
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Resource.LoadAssetsByLabelAsync<T>(label, cacheType, cts.Token);
    }

    public static void Release(string key) => Main.Resource.Release(key);
    public static void ReleaseLabel(string key) => Main.Resource.ReleaseLabel(key);
    #endregion

    #region Instantiate & Pool

    public static async UniTask<GameObject> SpawnAsync(
        string address, 
        Transform ts = null,
        CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Pool.SpawnAsync(address, cts.Token);
    }

    public static async UniTask<T> Instantiate<T>(string key,
        AssetCacheType casheType = AssetCacheType.NonRequired ,
        CancellationToken token = default) where T : Component
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        var ct = cts.Token;

        GameObject prefab = await Main.Resource.LoadAssetAsync<GameObject>(key, casheType, ct);
        if (prefab == null) return null;

        GameObject newObj = UnityEngine.Object.Instantiate(prefab);
        return newObj.GetComponent<T>();
    }

    public static void Destroy(GameObject go)
    {
        if (go == null) return;
        if (Main.Pool.InstanceToAddress.ContainsKey(go))
        {
            Main.Pool.Despawn(go);
        }
        else
        {
            UnityEngine.Object.Destroy(go);
        }
    }

    #endregion

    #region ChangeScene
    private static void RunStandardSceneCleanupTask()
    {
        if (Main.UI != null) Main.UI.ClearAll();
        if (Main.Input != null) Main.Input.SetInputActions(InputActionType.None);
        if (Main.Time != null) Main.Time.Clear();
        if (Main.Pool != null) Main.Pool.ClearAllPool();
        if (Main.Resource != null) Main.Resource.ClearNonRequired();
    }

    public static void ChangeScene(string sceneName, Func<UniTask> before = null, Func<UniTask> after = null, bool effect = true)
    {
        Main.Scene.ChangeSceneAsync(sceneName, async () => {
            RunStandardSceneCleanupTask();
            if (before != null) await before();
        }, after, effect).Forget();
    }
    #endregion

    #region UI
    public static async UniTask<T> ShowView<T>(string key, CancellationToken token = default) where T : UI_View
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.UI.ShowView<T>(key, cts.Token);
    }

    public static async UniTask<T> ShowPopup<T>(string key = null, bool clickGuard = false, float clickGuardAlpha = -1f, bool clickClose = false, CancellationToken token = default) where T : UI_Popup
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.UI.ShowPopup<T>(key, clickGuard, clickGuardAlpha, clickClose, cts.Token);
    }
    #endregion

    #region Sound
    public static void PlayBGM(AudioLibraryMusic key) => Main.JSAM.PlayBGM(key);
    public static void PlaySFX(AudioLibrarySounds key) => Main.JSAM.PlaySFX(key);

    #endregion

    #region Utils

    public static async UniTask SceneDelay(int milliseconds, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        await UniTask.Delay(milliseconds, cancellationToken: cts.Token);
    }
    public static T GetOrAddComponent<T>(this GameObject obj) where T : Component => Utilities.GetOrAddComponent<T>(obj);
    public static T FindChild<T>(this GameObject obj, string name = null) where T : Component => Utilities.FindChild<T>(obj, name);
    public static T FindChildDirect<T>(this GameObject obj, string name = null) where T : Component => Utilities.FindChildDirect<T>(obj, name);
    public static GameObject FindChild(this GameObject obj, string name = null) => Utilities.FindChild(obj, name);
    public static GameObject FindChildDirect(this GameObject obj, string name = null) => Utilities.FindChildDirect(obj, name);

    #endregion
}
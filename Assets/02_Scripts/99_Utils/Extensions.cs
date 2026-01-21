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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        return await Managers.Resource.LoadAssetAsync<T>(key, cacheType, cts.Token);
    }

    public static async UniTask<List<T>> LoadAssetsByLabelAsync<T>(string label, AssetCacheType cacheType = AssetCacheType.NonRequired, CancellationToken token = default) where T : UnityEngine.Object
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        return await Managers.Resource.LoadAssetsByLabelAsync<T>(label, cacheType, cts.Token);
    }

    public static void Release(string key) => Managers.Resource.Release(key);
    public static void ReleaseLabel(string key) => Managers.Resource.ReleaseLabel(key);
    #endregion

    #region Input
    public static bool IsPointerOverUI() => Managers.Input.IsPointerOverUI();
    public static bool IsPointerPressed() => Managers.Input.IsPointerPressed();
    public static Vector2 GetPointerPosition() => Managers.Input.GetPointerPosition();

    public static void Bind(this InputAction action, Action<InputAction.CallbackContext> started = null, Action<InputAction.CallbackContext> performed = null, Action<InputAction.CallbackContext> canceled = null)
        => Managers.Input.BindAction(action, started, performed, canceled);

    public static void Unbind(this InputAction action, Action<InputAction.CallbackContext> started = null, Action<InputAction.CallbackContext> performed = null, Action<InputAction.CallbackContext> canceled = null)
        => Managers.Input.UnBindAction(action, started, performed, canceled);

    public static void InputClear() => Managers.Input.ClearAll();
    #endregion

    #region Instantiate & Pool

    public static async UniTask<GameObject> SpawnAsync(
        string address, 
        CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        return await Managers.Pool.SpawnAsync(address, cts.Token);
    }

    public static async UniTask<T> Instantiate<T>(string key,
        AssetCacheType casheType = AssetCacheType.NonRequired ,
        CancellationToken token = default) where T : Component
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        var ct = cts.Token;

        GameObject prefab = await Managers.Resource.LoadAssetAsync<GameObject>(key, casheType, ct);
        if (prefab == null) return null;

        GameObject newObj = UnityEngine.Object.Instantiate(prefab);
        return newObj.GetComponent<T>();
    }

    public static void Destroy(GameObject go)
    {
        if (go == null) return;
        if (Managers.Pool.InstanceToAddress.ContainsKey(go))
        {
            Managers.Pool.Despawn(go);
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
        if (Managers.Audio != null) Managers.Audio.ClearSFX();
        if (Managers.UI != null) Managers.UI.ClearAll();
        if (Managers.Input != null) Managers.Input.ClearAll();
        if (Managers.Time != null) Managers.Time.Clear();
        if (Managers.Pool != null) Managers.Pool.ClearAllPool();
        if (Managers.Resource != null) Managers.Resource.ClearNonRequired();
    }

    public static void ChangeScene(string sceneName, Func<UniTask> before = null, Func<UniTask> after = null, bool effect = true)
    {
        Managers.Scene.ChangeSceneAsync(sceneName, async () => {
            RunStandardSceneCleanupTask();
            if (before != null) await before();
        }, after, effect).Forget();
    }
    #endregion

    #region UI
    public static async UniTask<T> ShowView<T>(string key, CancellationToken token = default) where T : UI_View
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        return await Managers.UI.ShowView<T>(key, cts.Token);
    }

    public static async UniTask<T> ShowPopup<T>(string key, bool clickGuard = false, float clickGuardAlpha = -1f, bool clickClose = false, CancellationToken token = default) where T : UI_Popup
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        return await Managers.UI.ShowPopup<T>(key, clickGuard, clickGuardAlpha, clickClose, cts.Token);
    }
    #endregion

    #region Sound
    public static void PlayBGM(string key) => Managers.Audio.PlayBgmAsync(key).Forget();
    public static void PlaySFX(string key) => Managers.Audio.PlaySfxAsync(key).Forget();
    public static void PlayOneShot(string key) => Managers.Audio.PlaySfxAsync(key, false).Forget();

    #endregion

    #region Utils

    public static async UniTask SceneDelay(int milliseconds, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Managers.Scene.CurrentToken);
        await UniTask.Delay(milliseconds, cancellationToken: cts.Token);
    }
    #endregion
}
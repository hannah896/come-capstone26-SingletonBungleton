using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public enum LoadingType
{
    Transition,
    Ads,
    Iap
}

/// <summary>
/// 로딩, 화면전환 등을 관리해주는 매니저
/// </summary>
public class LoadingManager : ContentManager
{
    #region Consts.

    private const float RetryTimeout = 3f;

    #endregion

    public bool IsLoadingSDK => _isLoadingSDK;

    #region Member

    private UI_Loader _loader;
    private UI_LoadingCanvas _loadingCanvas;
    private bool _isProcessing;
    private bool _isLoadingSDK;

    public HashSet<LoadingSDK> LoadingSDKs = new();
    
    #endregion

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        if (Main.IsEditorMode) return;
        
        _loader = Object.FindFirstObjectByType<UI_Loader>();
        if (!_loader)
        {
            GameObject go = await Main.Resource.LoadAssetAsync<GameObject>(nameof(UI_Loader));
            _loader = await Main.Resource.LoadAssetAsync<UI_Loader>();
            _loader = Object.Instantiate(_loader);
            _loader.gameObject.name = "UI_Loader";
            if(!_loader) Debug.LogError("Could not find UI_Loader");
        }
        
        _loadingCanvas = Object.FindFirstObjectByType<UI_LoadingCanvas>();
        if (!_loadingCanvas)
        {
            _loadingCanvas = await Extensions.ShowPopup<UI_LoadingCanvas>();
            if (!_loadingCanvas) Debug.LogError("Could not find UI_LoadingCanvas");
        }
            
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            EventSystem eventSystem = await Main.Resource.LoadAssetAsync<EventSystem>("EventSystem");
            if (!eventSystem) Debug.LogError($"Could not find EventSystem.");
        }
        
        if(_loadingCanvas) _loadingCanvas.Set();
    }

    public async void InitializeSDK()
    {
        foreach (LoadingSDK loadingSDK in LoadingSDKs)
        {
            try
            {
                _ = loadingSDK.InitializeSDK();
            }
            catch (Exception e)
            {
                Debug.LogError($"SDKInitialized Failed: {e.Message}");
            }
        }
        
        foreach (LoadingSDK loadingSDK in LoadingSDKs)
        {
            try
            {
                if (!await WaitUntilWithTimeout(loadingSDK.IsInitializedSDK, loadingSDK.GetType().ToString()))
                    Debug.LogError($"{loadingSDK.GetType()} initialization failed or timed out.");
                Debug.Log($"{loadingSDK.GetType()} initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"SDKInitializer FlowAsync Exception: {ex.Message}");
            }
        }
        _isLoadingSDK = true;
    }

    public void Show(LoadingType loadingType, Action onLoadingComplete = null)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        _loader.Set(loadingType, onLoadingComplete);
        _loader.Show();
    }

    public void Hide(float minWaitSec)
    {
        if (!_isProcessing) return;
        _isProcessing = false;
        _loader.Hide(minWaitSec);
    }

    public void Hide() => Hide(0);

    // 타임아웃 기능만 
    private async UniTask<bool> WaitUntilWithTimeout(Func<bool> initialized, string sdkName)
    {
        float startTimer = 0f;
        while (!initialized())
        {
            await UniTask.NextFrame();
            startTimer += Time.deltaTime;
            if (startTimer > RetryTimeout)
            {
                Debug.LogError($"Timeout {sdkName} : Over {RetryTimeout} seconds");
                return false;
            }
        }

        return true;
    }
}
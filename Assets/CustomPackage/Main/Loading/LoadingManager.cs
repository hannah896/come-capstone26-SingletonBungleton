using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SDK 초기화 및 로딩 화면을 관리하는 매니저.
/// 여러 SDK의 비동기 초기화를 조율합니다.
/// </summary>
public class LoadingManager : CoreManager
{
    #region Constants

    // SDK 초기화 타임아웃 (초)
    private const float RetryTimeout = 3f;

    #endregion

    #region Fields

    // SDK 로딩 완료 여부
    private bool _isLoadingSDK;

    #endregion

    #region Properties

    // SDK 로딩 완료 여부
    public bool IsLoadingSDK => _isLoadingSDK;

    // 초기화할 SDK 목록
    public HashSet<LoadingSDK> LoadingSDKs { get; } = new();

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        if (Main.IsEditorMode) return;

        var _loadingCanvas = UnityEngine.Object.FindFirstObjectByType<UI_LoadingCanvas>();
        if (!_loadingCanvas)
        {
            _loadingCanvas = await Extensions.ShowPopup<UI_LoadingCanvas>();
            if (!_loadingCanvas) Debug.LogError("Could not find UI_LoadingCanvas");
        }
        _loadingCanvas.Set();
    }

    /// <summary>
    /// 모든 등록된 SDK를 비동기로 초기화합니다.
    /// </summary>
    public async void InitializeSDKsAsync()
    {
        foreach (LoadingSDK loadingSDK in LoadingSDKs)
        {
            try
            {
                _ = loadingSDK.InitializeSDK();
            }
            catch (Exception e)
            {
                Debug.LogError($"SDK Initialized Start Failed: {e.Message}");
            }
        }

        foreach (LoadingSDK loadingSDK in LoadingSDKs)
        {
            try
            {
                bool success = await WaitUntilWithTimeout(() => loadingSDK.IsInitializedSDK(), loadingSDK.GetType().Name);

                if (!success)
                {
                    Debug.LogWarning($"{loadingSDK.GetType().Name} initialization timed out.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"SDKInitializer Flow Exception: {ex.Message}");
            }
        }

        _isLoadingSDK = true;
    }

    #endregion

    #region Internal Methods

    // 타임아웃과 함께 조건 대기
    private async UniTask<bool> WaitUntilWithTimeout(Func<bool> condition, string sdkName)
    {
        try
        {
            await UniTask.WaitUntil(condition).Timeout(TimeSpan.FromSeconds(RetryTimeout));
            return true;
        }
        catch (TimeoutException)
        {
            Debug.LogError($"[Timeout] {sdkName} : Failed to initialize within {RetryTimeout}s");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion
}

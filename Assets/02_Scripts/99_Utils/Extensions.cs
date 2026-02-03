using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Main 매니저들의 자주 사용하는 메서드들을 정적 확장 메서드로 제공하는 유틸리티 클래스.
/// CancellationToken은 자동으로 씬 전환 토큰과 연결됩니다.
/// </summary>
public static class Extensions
{
    #region Resource

    /// <summary>
    /// 에셋을 비동기로 로드합니다.
    /// </summary>
    public static async UniTask<T> LoadAssetAsync<T>(
        string key,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken token = default) where T : UnityEngine.Object
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Resource.LoadAssetAsync<T>(key, cacheType, cts.Token);
    }

    /// <summary>
    /// 라벨로 에셋들을 비동기로 로드합니다.
    /// </summary>
    public static async UniTask<List<T>> LoadAssetsByLabelAsync<T>(
        string label,
        AssetCacheType cacheType = AssetCacheType.NonRequired,
        CancellationToken token = default) where T : UnityEngine.Object
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Resource.LoadAssetsByLabelAsync<T>(label, cacheType, cts.Token);
    }

    /// <summary>
    /// 로드된 에셋을 해제합니다.
    /// </summary>
    public static void Release(string key) => Main.Resource.Release(key);

    /// <summary>
    /// 라벨로 로드된 에셋들을 해제합니다.
    /// </summary>
    public static void ReleaseLabel(string key) => Main.Resource.ReleaseLabel(key);

    #endregion

    #region Pool

    /// <summary>
    /// 일반 오브젝트를 생성합니다.
    /// </summary>
    /// <param name="key">어드레서블 키값</param>
    /// <param name="casheType">지속 여부</param>
    /// <param name="token">캔슬 토큰</param>
    /// <typeparam name="T">가져올 타입</typeparam>
    public static async UniTask<T> Instantiate<T>(
        string key = null,
        AssetCacheType casheType = AssetCacheType.NonRequired ,
        CancellationToken token = default) where T : Component
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        var ct = cts.Token;
        if (string.IsNullOrEmpty(key)) key = typeof(T).Name;
        
        GameObject prefab = await Main.Resource.LoadAssetAsync<GameObject>(key, casheType, ct);
        if (prefab == null)
        {
            prefab = new GameObject(key);
            var comp = prefab.AddComponent<T>();
            return comp;
        }
        else
        {
            GameObject newObj = UnityEngine.Object.Instantiate(prefab);
            return newObj.GetComponent<T>();
        }
    }
    
    /// <summary>
    /// 풀에서 오브젝트를 스폰합니다.
    /// </summary>
    public static async UniTask<GameObject> SpawnAsync(
        string address,
        Transform parent = null,
        CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Pool.SpawnAsync(address, parent, cts.Token);
    }

    /// <summary>
    /// 풀에서 특정 컴포넌트가 있는 오브젝트를 스폰합니다.
    /// </summary>
    public static async UniTask<T> SpawnAsync<T>(
        string address,
        Transform parent = null,
        CancellationToken token = default) where T : Component
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.Pool.SpawnAsync<T>(address, parent, cts.Token);
    }

    /// <summary>
    /// 오브젝트를 풀로 반환합니다.
    /// </summary>
    public static void Despawn(GameObject go) => Main.Pool.Despawn(go);

    #endregion

    #region UI - Hud

    /// <summary>
    /// Hud UI를 표시합니다.
    /// </summary>
    public static async UniTask<T> ShowHud<T>(
        string key = null,
        CancellationToken token = default) where T : UI_Hud
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.UI.ShowHud<T>(key, cts.Token);
    }

    /// <summary>
    /// 현재 Hud를 닫습니다.
    /// </summary>
    public static void CloseHud() => Main.UI.CloseHud();

    #endregion

    #region UI - Popup

    /// <summary>
    /// 팝업 UI를 표시합니다.
    /// </summary>
    public static async UniTask<T> ShowPopup<T>(
        string key = null,
        bool clickGuard = false,
        float clickGuardAlpha = -1f,
        bool clickClose = false,
        CancellationToken token = default) where T : UI_Popup
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        return await Main.UI.ShowPopup<T>(key, clickGuard, clickGuardAlpha, clickClose, cts.Token);
    }

    /// <summary>
    /// 특정 팝업을 닫습니다.
    /// </summary>
    public static void ClosePopup(UI_Popup popup) => Main.UI.ClosePopup(popup);

    /// <summary>
    /// 가장 위의 팝업을 닫습니다.
    /// </summary>
    public static void CloseTopPopup(bool withAnimation = true) => Main.UI.CloseTopPopup(withAnimation);

    /// <summary>
    /// 모든 팝업을 닫습니다.
    /// </summary>
    public static void CloseAllPopups(bool withAnimation = false) => Main.UI.CloseAllPopups(withAnimation);

    #endregion

    #region UI - Screen

    /// <summary>
    /// 화면 효과를 표시합니다.
    /// </summary>
    public static void ShowScreen(ScreenEffectType type, Action act = null) => Main.UI.ShowScreen(type, act);

    /// <summary>
    /// 화면 효과를 숨깁니다.
    /// </summary>
    public static void HideScreen(float time = 3f) => Main.UI.HideScreen(time);

    /// <summary>
    /// 화면 효과를 비동기로 표시합니다.
    /// </summary>
    public static async UniTask ShowScreenAsync(ScreenEffectType type, Action act = null, CancellationToken token = default)
        => await Main.UI.ShowScreenAsync(type, act, token);

    /// <summary>
    /// 화면 효과를 비동기로 숨깁니다.
    /// </summary>
    public static async UniTask HideScreenAsync(float time = 3f) => await Main.UI.HideScreenAsync(time);

    #endregion

    #region Scene

    /// <summary>
    /// 씬을 변경합니다.
    /// </summary>
    public static void ChangeScene(string sceneName, Func<UniTask> before = null, Func<UniTask> after = null)
    {
        Main.Scene.ChangeSceneAsync(sceneName, async () =>
        {
            RunStandardSceneCleanupTask();
            if (before != null) await before();
        }, after).Forget();
    }

    /// <summary>
    /// 현재 씬을 다시 로드합니다.
    /// </summary>
    public static void ReloadScene() => Main.Scene.Reload();

    // 씬 정리 작업 실행
    private static void RunStandardSceneCleanupTask()
    {
        Main.UI?.Clear();
        Main.Time?.Clear();
        Main.Pool?.Clear();
        Main.Resource?.ClearNonRequired();
    }

    #endregion

    #region Sound

    /// <summary>
    /// 배경음을 재생합니다.
    /// </summary>
    public static void PlayBGM(AudioLibraryMusic key) => Main.JSAM.PlayBGM(key);

    /// <summary>
    /// 효과음을 재생합니다.
    /// </summary>
    public static void PlaySFX(AudioLibrarySounds key) => Main.JSAM.PlaySFX(key);

    #endregion

    #region Input

    /// <summary>
    /// 단일 입력 액션 타입을 활성화합니다.
    /// </summary>
    public static void SetInput<T>() where T : InputActions => Main.Input.SetInput<T>();

    /// <summary>
    /// 두 개의 입력 액션 타입을 활성화합니다.
    /// </summary>
    public static void SetInput<T1, T2>()
        where T1 : InputActions
        where T2 : InputActions
        => Main.Input.SetInput<T1, T2>();

    /// <summary>
    /// 세 개의 입력 액션 타입을 활성화합니다.
    /// </summary>
    public static void SetInput<T1, T2, T3>()
        where T1 : InputActions
        where T2 : InputActions
        where T3 : InputActions
        => Main.Input.SetInput<T1, T2, T3>();

    /// <summary>
    /// 입력 액션을 추가합니다.
    /// </summary>
    public static void AddInput<T>() where T : InputActions => Main.Input.AddInput<T>();

    /// <summary>
    /// 입력 액션을 제거합니다.
    /// </summary>
    public static void RemoveInput<T>() where T : InputActions => Main.Input.RemoveInput<T>();

    /// <summary>
    /// 모든 입력 액션을 제거합니다.
    /// </summary>
    public static void RemoveAllInputs() => Main.Input.RemoveAllInputs();

    /// <summary>
    /// 입력 액션이 활성화 상태인지 확인합니다.
    /// </summary>
    public static bool IsInputActive<T>() where T : InputActions => Main.Input.IsActive<T>();

    /// <summary>
    /// 캐싱된 액션 인스턴스를 가져옵니다.
    /// </summary>
    public static T GetAction<T>() where T : InputActions => Main.Input.GetAction<T>();

    /// <summary>
    /// 캐싱된 액션 인스턴스를 가져오거나, 없으면 생성합니다.
    /// </summary>
    public static T GetOrCreateAction<T>() where T : InputActions => Main.Input.GetOrCreateAction<T>();

    /// <summary>
    /// 현재 포인터가 UI 위에 있는지 확인합니다.
    /// </summary>
    public static bool IsPointerOverUI(Vector2 screenPos) => Main.Input.IsPointerOverUI(screenPos);

    /// <summary>
    /// 스크린 좌표를 월드 좌표로 변환합니다.
    /// </summary>
    public static Vector3 ScreenToWorld(Vector2 screenPos) => Main.Input.ScreenToWorld(screenPos);

    #endregion

    #region Localization

    /// <summary>
    /// 키값에 해당하는 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public static string GetLocalString(string localKey) => Main.Local.GetLocalString(localKey);

    /// <summary>
    /// ELocalizedName enum으로 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public static string GetLocalString(ELocalizedName localizedName, string originalText = "[Localize failed]")
        => Main.Local.GetLocalString(localizedName, originalText);

    /// <summary>
    /// 포맷이 적용된 로컬 번역 문자열을 반환합니다.
    /// </summary>
    public static string GetFormattedLocalString(string key, params object[] args)
        => Main.Local.GetFormattedLocalString(key, args);

    /// <summary>
    /// 언어를 변경합니다.
    /// </summary>
    public static void ChangeLocale(LocalizedCountryType type = LocalizedCountryType.None)
        => Main.Local.ChangeLocale(type);

    #endregion

    #region Loop

    /// <summary>
    /// 게임 속도에 맞춰 동작하는 시퀀스를 생성합니다.
    /// </summary>
    public static Sequence GetGameSequence() => Main.Loop.GetGameSequence();

    /// <summary>
    /// 게임 속도를 설정합니다.
    /// </summary>
    public static void SetTimeScale(float timeScale) => Main.Loop.SetGameSpeed(timeScale);

    /// <summary>
    /// 슬로우 모션 효과를 적용합니다.
    /// </summary>
    public static void DoSlowMotion(float targetScale, float duration)
        => Main.Loop.DoFadeGameSpeed(targetScale, duration).Forget();

    #endregion

    #region Data

    /// <summary>
    /// 키로 데이터를 가져옵니다.
    /// </summary>
    public static T GetData<T>(string key) where T : Data => Main.Data.Get<T>(key);

    /// <summary>
    /// 특정 타입의 모든 데이터를 가져옵니다.
    /// </summary>
    public static List<T> GetAllData<T>() where T : Data => Main.Data.GetAll<T>();

    /// <summary>
    /// 스테이지 데이터를 가져옵니다.
    /// </summary>
    public static StageData GetStageData(int stage) => Main.Data.GetStageData(stage);

    /// <summary>
    /// 특정 키가 존재하는지 확인합니다.
    /// </summary>
    public static bool ContainsDataKey<T>(string key) where T : Data => Main.Data.ContainsKey<T>(key);

    #endregion

    #region Utils

    /// <summary>
    /// 씬 전환 토큰과 연결된 딜레이를 실행합니다.
    /// </summary>
    public static async UniTask SceneDelay(int milliseconds, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        await UniTask.Delay(milliseconds, cancellationToken: cts.Token);
    }

    /// <summary>
    /// 씬 전환 토큰과 연결된 딜레이를 실행합니다 (초 단위).
    /// </summary>
    public static async UniTask SceneDelaySeconds(float seconds, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, Main.Scene.CurrentToken);
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: cts.Token);
    }

    #endregion

    #region GameObject Extensions

    /// <summary>
    /// 컴포넌트를 가져오거나, 없으면 추가합니다.
    /// </summary>
    public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        => Utilities.GetOrAddComponent<T>(obj);

    /// <summary>
    /// 자식 중에서 컴포넌트를 찾습니다.
    /// </summary>
    public static T FindChild<T>(this GameObject obj, string name = null) where T : Component
        => Utilities.FindChild<T>(obj, name);

    /// <summary>
    /// 직접 자식 중에서 컴포넌트를 찾습니다.
    /// </summary>
    public static T FindChildDirect<T>(this GameObject obj, string name = null) where T : Component
        => Utilities.FindChildDirect<T>(obj, name);

    /// <summary>
    /// 자식 중에서 게임오브젝트를 찾습니다.
    /// </summary>
    public static GameObject FindChild(this GameObject obj, string name = null)
        => Utilities.FindChild(obj, name);

    /// <summary>
    /// 직접 자식 중에서 게임오브젝트를 찾습니다.
    /// </summary>
    public static GameObject FindChildDirect(this GameObject obj, string name = null)
        => Utilities.FindChildDirect(obj, name);

    #endregion
}

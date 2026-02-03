using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 기본 추상 클래스.
/// 씬 진입/퇴장 로직을 정의합니다.
/// </summary>
public abstract class SceneBase
{
    public abstract UniTask EnterScene(CancellationToken token);
    public abstract void ExitScene();
}

/// <summary>
/// 씬 전환 및 관리를 담당하는 매니저.
/// 비동기 씬 로딩과 전환 애니메이션을 처리합니다.
/// </summary>
public class SceneManagerEx : ContentManager
{
    #region Fields

    // 현재 활성화된 씬
    private SceneBase _currentScene;

    // 씬 전환용 취소 토큰 소스
    private CancellationTokenSource _cts = new();

    // 씬 전환 중 여부
    private bool _isTransitioning = false;

    #endregion

    #region Properties

    // 현재 씬
    public SceneBase Current => _currentScene;

    // 현재 취소 토큰
    public CancellationToken CurrentToken => _cts.Token;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        string sceneName = SceneManager.GetActiveScene().name;
        await CreateAndEnterScene(sceneName, _cts.Token);
    }

    #endregion

    #region Scene Loading

    /// <summary>
    /// 모든 매니저를 정리합니다.
    /// </summary>
    public void Cleanup() => Main.Clear();

    /// <summary>
    /// 지정된 씬을 로드합니다.
    /// </summary>
    public void Load(string sceneName) => ChangeScene(sceneName);

    /// <summary>
    /// 현재 씬을 다시 로드합니다.
    /// </summary>
    public void Reload() => ChangeScene(SceneManager.GetActiveScene().name);

    /// <summary>
    /// 씬을 변경합니다.
    /// </summary>
    public void ChangeScene(string sceneName, Func<UniTask> before = null, Func<UniTask> after = null)
    {
        ChangeSceneAsync(sceneName, before, after).Forget();
    }

    /// <summary>
    /// 씬을 비동기로 변경합니다.
    /// </summary>
    public async UniTask ChangeSceneAsync(
        string sceneName,
        Func<UniTask> onBeforeLoad = null,
        Func<UniTask> onAfterLoad = null)
    {
        if (_isTransitioning || string.IsNullOrEmpty(sceneName)) return;
        _isTransitioning = true;

        _cts?.Cancel(); // 이전 유니테스크 작업들 모두 취소
        _cts?.Dispose(); // 테스크 메모리 해제
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Main.UI.ShowScreenAsync(ScreenEffectType.Transition);
            await UniTask.Delay(200, cancellationToken: token);

            Cleanup();
            if (onBeforeLoad != null) await onBeforeLoad().AttachExternalCancellation(token);
            _currentScene?.ExitScene();

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            op.allowSceneActivation = true;
            await op.ToUniTask(cancellationToken: token);

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid()) SceneManager.SetActiveScene(loadedScene);

            await UnloadOldScenes(sceneName, token);

            if (onAfterLoad != null) await onAfterLoad().AttachExternalCancellation(token);
            await CreateAndEnterScene(sceneName, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _isTransitioning = false;
            Main.UI.HideScreenAsync();
        }
    }

    #endregion

    #region Internal Methods

    // 이전 씬들을 언로드
    private async UniTask UnloadOldScenes(string currentSceneName, CancellationToken token)
    {
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == currentSceneName || scene.name == "InitScene") continue;

            if (scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask(cancellationToken: token);
            }
        }
    }

    // 씬 객체 생성 및 진입
    private async UniTask CreateAndEnterScene(string sceneName, CancellationToken token)
    {
        Type sceneType = Type.GetType(sceneName);
        if (sceneType != null && typeof(SceneBase).IsAssignableFrom(sceneType))
        {
            _currentScene = Activator.CreateInstance(sceneType) as SceneBase;
            if (_currentScene != null) await _currentScene.EnterScene(token);
        }
    }

    #endregion
}

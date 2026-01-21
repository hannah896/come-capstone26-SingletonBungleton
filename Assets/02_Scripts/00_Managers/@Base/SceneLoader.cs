using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.SceneManagement;

public interface ISceneBase
{
    UniTask EnterScene(CancellationToken token);
    void ExitScene();
}

public class SceneBase : ISceneBase
{
    public virtual async UniTask EnterScene(CancellationToken token)
    {
        await UniTask.Yield();
    }

    public virtual void ExitScene()
    {

    }
}

public class SceneLoader
{
    private CancellationTokenSource _cts = new();
    private ISceneBase _currentScene;
    private bool _isTransitioning = false;

    public CancellationToken CurrentToken => _cts.Token;

    public async UniTask Init()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        await CreateAndEnterScene(sceneName, _cts.Token);
    }

    public async UniTask ChangeSceneAsync(
        string sceneName,
        Func<UniTask> onBeforeLoad = null,
        Func<UniTask> onAfterLoad = null,
        bool useEffect = false)
    {
        if (_isTransitioning || string.IsNullOrEmpty(sceneName)) return;
        _isTransitioning = true;

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Managers.UI.SceneFadeAsync(0f, 1f, token);

            if (onBeforeLoad != null)
                await onBeforeLoad().AttachExternalCancellation(token);

            _currentScene?.ExitScene();

            await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: token);

            if (onAfterLoad != null)
                await onAfterLoad().AttachExternalCancellation(token);

            await CreateAndEnterScene(sceneName, token);

            await Managers.UI.SceneFadeAsync(1f, 0f, token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"[Scene] {sceneName} transition canceled.");
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private async UniTask CreateAndEnterScene(string sceneName, CancellationToken token)
    {
        Type sceneType = Type.GetType(sceneName);
        if (sceneType != null && typeof(ISceneBase).IsAssignableFrom(sceneType))
        {
            _currentScene = Activator.CreateInstance(sceneType) as ISceneBase;
            if (_currentScene != null)
            {
                await _currentScene.EnterScene(token);
            }
        }
        else
        {
            Debug.LogWarning($"[Scene] No ISceneBase implementation found for: {sceneName}");
        }
    }
}

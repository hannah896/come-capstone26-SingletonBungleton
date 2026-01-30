using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬의 이동 및 초기화를 관리해주는 매니저
/// </summary>
public class SceneManagerEx : CoreManager {

    public SceneBase Current { get; set; }
    public UI_Scene SceneUI { get; set; }
    private CancellationTokenSource _cts = new();
    public CancellationToken CurrentToken => _cts.Token;
    private SceneBase _currentScene;
    private bool _isTransitioning = false;
    
    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        string sceneName = SceneManager.GetActiveScene().name;
        await CreateAndEnterScene(sceneName, _cts.Token);
    }


    public void Load(string sceneName) {
        Main.Clear();
        SceneManager.LoadScene(sceneName);
    }

    public void SwitchAsync(string sceneName, bool isTransition = true) {
        if(isTransition) Main.StartCoroutine(SwitchSceneAsync(sceneName));
        else Main.StartCoroutine(LoadSceneAsync(sceneName));
    }
    
    public void Reload() {
        Main.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public IEnumerator LoadSceneAsync(string sceneName, Action<float> onProgress = null) {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f) {
            onProgress?.Invoke(operation.progress / 0.9f);
            yield return null;
        }
        onProgress?.Invoke(1f);

        void OnLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.name != sceneName) return;
            SceneManager.SetActiveScene(scene);
            SceneManager.sceneLoaded -= OnLoaded;
        }
        SceneManager.sceneLoaded += OnLoaded;
        
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
    }

    private IEnumerator SwitchSceneAsync(string sceneName, Action<float> onProgress = null) {
        // #1. 로딩 보이기.
        Main.Loading.Show(LoadingType.Transition);
        yield return new WaitForSeconds(0.2f);
        
        // #2. 클리어.
        Main.Clear();
        
        // #3. 새 씬 로드.
        yield return LoadSceneAsync(sceneName, onProgress);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        
        // #4. 이전 씬 언로드.
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName) continue;
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        
        // #5. 로딩 숨기기.
        Main.Loading.Hide();
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
            await Main.UI.SceneFadeAsync(0f, 1f, token);

            if (onBeforeLoad != null)
                await onBeforeLoad().AttachExternalCancellation(token);

            _currentScene?.ExitScene();

            await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: token);

            if (onAfterLoad != null)
                await onAfterLoad().AttachExternalCancellation(token);

            await CreateAndEnterScene(sceneName, token);

            await Main.UI.SceneFadeAsync(1f, 0f, token);
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
        if (sceneType != null && typeof(SceneBase).IsAssignableFrom(sceneType))
        {
            _currentScene = Activator.CreateInstance(sceneType) as SceneBase;
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

public abstract class SceneBase
{
    public abstract UniTask EnterScene(CancellationToken token);
    public abstract void ExitScene();
}

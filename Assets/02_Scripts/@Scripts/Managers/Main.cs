using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    #region Singleton

    private static Main _instance;

    // 초기화 완료 여부
    private static bool _initialized;

    public static Main Instance
    {
        get
        {
            if (_instance == null) Initialize();
            return _instance;
        }
    }

    #endregion

    #region Static Properties - Primary Managers

    public static ResourceManager Resource => Instance?._resource;
    public static DataManager Data => Instance?._data;
    public static UIManager UI => Instance?._ui;
    public static JSAMManager JSAM => Instance?._jsam;
    #endregion

    #region Static Properties - Core Managers

    public static PoolManager Pool => Instance?._pool;
    public static ScreenManager Screen => Instance?._screen;
    public static LoopManager Loop => Instance?._loop;
    public static LocalizationManager Local => Instance?._local;
    public static TimeManager Time => Instance?._time;
    public static LoadingManager Loading => Instance?._loading;
    public static InputManager Input => Instance?._input;
    public static TextManager Text => Instance?._text;
    #endregion
    // Content.
    public static BoardManager Board => Instance?._board;
    public static SceneManagerEx Scene => Instance?._scene;

    #endregion

    #region Fields

    public static bool IsEditorMode = false;

    // Primary.
    private readonly ResourceManager _resource = new();
    private readonly DataManager _data = new();

    private readonly UIManager _ui = new();
    private readonly JSAMManager _jsam = new();

    // Core.
    private readonly PoolManager _pool = new();
    private readonly ScreenManager _screen = new();
    private readonly LoopManager _loop = new();
    private readonly LocalizationManager _local = new();

    private readonly TimeManager _time = new();
    private readonly InputManager _input = new();
    private readonly LoadingManager _loading = new();
    private readonly TextManager _text = new();

    // Content.
    private readonly BoardManager _board = new();
    private readonly SceneManagerEx _scene = new();

    private static readonly List<PrimaryManager> PrimaryManagers = new();
    private static readonly List<CoreManager> CoreManagers = new();
    private static readonly List<ContentManager> ContentManagers = new();

    #endregion

    #region Initialize

    private static async void Initialize()
    {
        if (_instance != null || _initialized) return;
        _initialized = true;

        Application.targetFrameRate = 60; // 프레임 60 고정
        QualitySettings.vSyncCount = 0; // 프레임 속도 향상
        DOTween.Init(true, true);

        GameObject obj = GameObject.Find("@Main");

        if (obj == null)
        {
            obj = new("@Main");
            _instance = obj.AddComponent<Main>();
        }

        DontDestroyOnLoad(obj);
        _instance = obj.GetComponent<Main>();

        // 우선 순위별 매니저 등록
        foreach (FieldInfo fieldInfo in typeof(Main).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (fieldInfo.GetValue(_instance) is PrimaryManager primaryManager)
            {
                PrimaryManagers.Add(primaryManager);
            }
            else if (fieldInfo.GetValue(_instance) is CoreManager coreManager)
            {
                CoreManagers.Add(coreManager);
            }
            else if (fieldInfo.GetValue(_instance) is ContentManager contentManager)
            {
                ContentManagers.Add(contentManager);
            }
        }

        // 우선순위 맞춰서 매니저 초기화
        var primaryTasks = PrimaryManagers
            .Select(task => task.Initialize())
            .ToArray();
        await UniTask.WhenAll(primaryTasks);

        var coreTasks = CoreManagers
            .Select(task => task.Initialize())
            .ToArray();
        await UniTask.WhenAll(coreTasks);

        var contentTasks = ContentManagers
            .Select(task => task.Initialize())
            .ToArray();
        await UniTask.WhenAll(contentTasks);

        if (!IsEditorMode)
        {
            Loading.InitializeSDK();
        }
    }

    #endregion

    #region MonoBehaviours

    private void Update()
    {
        if (!Loop.IsInitialized) return;
        Loop.Update(UnityEngine.Time.deltaTime);
        Loop.GameUpdate(UnityEngine.Time.deltaTime);
    }
    #endregion

    #region CoroutineHelper

    public new static Coroutine StartCoroutine(IEnumerator coroutine) =>
        (Instance as MonoBehaviour).StartCoroutine(coroutine);

    public new static void StopCoroutine(Coroutine coroutine) => (Instance as MonoBehaviour).StopCoroutine(coroutine);

    #endregion

    public static void Clear()
    {
        foreach (FieldInfo fieldInfo in typeof(Main).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            CoreManager manager = fieldInfo.GetValue(_instance) as CoreManager;
            manager?.Clear();
        }

        foreach (FieldInfo fieldInfo in typeof(Main).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            ContentManager manager = fieldInfo.GetValue(_instance) as ContentManager;
            manager?.Clear();
        }
    }
}

#region SubClass

public abstract class Managers
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    public bool IsInitialized { get; private set; }

    public async UniTask Initialize() // 락을 위해 virtual을 뺌
    {
        if (IsInitialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            // 자식들이 구현할 실제 내용물만 따로 부름
            await OnInitializeAsync();

            IsInitialized = true;
        }
        finally { _initLock.Release(); }
    }

    // 실제 Manager에서 비동기로 초기화할 함수
    protected virtual async UniTask OnInitializeAsync()
    {
        await UniTask.CompletedTask;
    }

    public virtual void Clear() { }
}

public abstract class PrimaryManager : Managers { }
public abstract class CoreManager : Managers { }
public abstract class ContentManager : Managers { }

#endregion

public static class BlossomPath
{
    public static readonly string RESOURCES_DATA = $"Data";
    public static readonly string RESOURCES_STAGEDATA = $"StageData";
}

public static class Styles
{
    public static readonly Color BUTTONCOLOR_DISABLED = new(100 / 255f, 100 / 255f, 100 / 255f, 1);
}
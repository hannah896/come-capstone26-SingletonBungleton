using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임의 핵심 진입점이자 모든 매니저의 중앙 관리자.
/// 싱글톤 패턴으로 구현되며, 매니저들의 초기화 순서를 제어합니다.
/// </summary>
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
    public static LoadingAnalyticsSDK AnalyticsSDK => Instance?._analyticsSDK;
    public static LoopManager Loop => Instance?._loop;
    public static LocalizationManager Local => Instance?._local;
    public static TimeManager Time => Instance?._time;
    public static LoadingManager Loading => Instance?._loading;
    public static InputManager Input => Instance?._input;
    public static TextManager Text => Instance?._text;
    public static CommandManager Command => Instance?._command;
    public static SimulationManager Simulation => Instance?._simulation;
    public static NetworkManager Network => Instance?._network;

    #endregion

    #region Static Properties - Content Managers
    public static GameManager Game => Instance?._game;
    public static SceneManagerEx Scene => Instance?._scene;

    #endregion

    #region Fields

    // 에디터 모드 여부 (SDK 초기화 스킵용)
    public static bool IsEditorMode = false;

    // Primary Managers
    private readonly ResourceManager _resource = new();
    private readonly DataManager _data = new();
    private readonly UIManager _ui = new();
    private readonly JSAMManager _jsam = new();

    // Core Managers
    private readonly PoolManager _pool = new();
    private readonly ScreenManager _screen = new();
    private readonly LoadingAnalyticsSDK _analyticsSDK = new();
    private readonly LoopManager _loop = new();
    private readonly LocalizationManager _local = new();
    private readonly TimeManager _time = new();
    private readonly InputManager _input = new();
    private readonly LoadingManager _loading = new();
    private readonly TextManager _text = new();
    private readonly CommandManager _command = new();
    private readonly SimulationManager _simulation = new();
    private readonly NetworkManager _network = new();

    // Content Managers
    private readonly GameManager _game = new();
    private readonly SceneManagerEx _scene = new();

    // 매니저 리스트 (초기화 순서 관리용)
    private static readonly List<PrimaryManager> PrimaryManagers = new();
    private static readonly List<CoreManager> CoreManagers = new();
    private static readonly List<ContentManager> ContentManagers = new();

    #endregion

    #region Initialization

    // Main 인스턴스 및 모든 매니저 초기화
    private static async void Initialize()
    {
        if (_instance != null || _initialized) return;
        _initialized = true;

        SetupApplication();
        CreateMainGameObject();
        CollectManagers();
        await InitializeAllManagers();
        PostInitialize();
    }

    // 애플리케이션 기본 설정
    private static void SetupApplication()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        DOTween.Init(true, true);

        if (SceneManager.GetActiveScene().name == "EditorScene")
        {
            IsEditorMode = true;
        }
    }

    // @Main 게임오브젝트 생성 및 설정
    private static void CreateMainGameObject()
    {
        GameObject obj = GameObject.Find("@Main");
        if (obj == null)
        {
            obj = new GameObject("@Main");
            obj.AddComponent<Main>();
        }

        DontDestroyOnLoad(obj);
        _instance = obj.GetComponent<Main>();
    }

    // 리플렉션을 통해 모든 매니저 인스턴스 수집
    private static void CollectManagers()
    {
        foreach (FieldInfo fieldInfo in typeof(Main).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            object value = fieldInfo.GetValue(_instance);

            if (value is PrimaryManager primaryManager)
            {
                PrimaryManagers.Add(primaryManager);
            }
            else if (value is CoreManager coreManager)
            {
                CoreManagers.Add(coreManager);
            }
            else if (value is ContentManager contentManager)
            {
                ContentManagers.Add(contentManager);
            }
        }
    }

    // 모든 매니저를 순서대로 초기화 (Primary → Core → Content)
    private static async UniTask InitializeAllManagers()
    {
        var primaryTasks = PrimaryManagers.Select(m => m.Initialize()).ToArray();
        await UniTask.WhenAll(primaryTasks);

        var coreTasks = CoreManagers.Select(m => m.Initialize()).ToArray();
        await UniTask.WhenAll(coreTasks);

        var contentTasks = ContentManagers.Select(m => m.Initialize()).ToArray();
        await UniTask.WhenAll(contentTasks);
    }

    // 초기화 완료 후 추가 작업
    private static void PostInitialize()
    {
        if (!IsEditorMode)
        {
            Loading.InitializeSDKsAsync();
        }
    }

    #endregion

    #region MonoBehaviour Callbacks

    // 매 프레임 업데이트
    private void Update()
    {
        if (!Loop.IsInitialized) return;

        Loop.Update(UnityEngine.Time.deltaTime);
        Loop.GameUpdate(UnityEngine.Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!Loop.IsInitialized) return;
        Loop.LateUpdate(UnityEngine.Time.deltaTime);
    }
    #endregion

    #region Coroutine Helpers

    /// <summary>
    /// 코루틴을 시작합니다.
    /// </summary>
    public new static Coroutine StartCoroutine(IEnumerator coroutine)
    {
        return (Instance as MonoBehaviour).StartCoroutine(coroutine);
    }

    /// <summary>
    /// 실행 중인 코루틴을 중지합니다.
    /// </summary>
    public new static void StopCoroutine(Coroutine coroutine)
    {
        (Instance as MonoBehaviour).StopCoroutine(coroutine);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// 모든 매니저의 Clear 메서드를 호출하여 리소스를 정리합니다.
    /// </summary>
    public static void Clear()
    {
        foreach (FieldInfo fieldInfo in typeof(Main).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (fieldInfo.GetValue(_instance) is Managers manager)
            {
                manager.Clear();
            }
        }
    }

    #endregion
}

#region Manager Base Classes

/// <summary>
/// 모든 매니저의 기본 클래스.
/// 세마포어를 통한 스레드 안전 초기화를 제공합니다.
/// </summary>
public abstract class Managers
{
    // 동시 초기화 방지용 세마포어
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // 초기화 완료 여부
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 매니저를 초기화합니다. 세마포어로 중복 초기화를 방지합니다.
    /// </summary>
    public async UniTask Initialize()
    {
        if (IsInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            await OnInitializeAsync();
            IsInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // 실제 초기화 로직을 구현하는 메서드
    protected virtual async UniTask OnInitializeAsync()
    {
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 매니저의 리소스를 정리합니다.
    /// </summary>
    public virtual void Clear() { }
}

/// <summary>
/// Primary 단계에서 초기화되는 매니저의 기본 클래스.
/// Resource, Data 등 기반 시스템에 사용됩니다.
/// </summary>
public abstract class PrimaryManager : Managers { }

/// <summary>
/// Core 단계에서 초기화되는 매니저의 기본 클래스.
/// UI, Input, Pool 등 인프라 시스템에 사용됩니다.
/// </summary>
public abstract class CoreManager : Managers { }

/// <summary>
/// Content 단계에서 초기화되는 매니저의 기본 클래스.
/// Board, Lives 등 게임별 로직에 사용됩니다.
/// </summary>
public abstract class ContentManager : Managers { }

#endregion

#region Constants

/// <summary>
/// 리소스 경로 상수 정의
/// </summary>
public static class BlossomPath
{
    public static readonly string RESOURCES_DATA = "Data";
    public static readonly string RESOURCES_STAGEDATA = "StageData";
}

/// <summary>
/// UI 스타일 상수 정의
/// </summary>
public static class Styles
{
    public static readonly Color BUTTONCOLOR_DISABLED = new(100 / 255f, 100 / 255f, 100 / 255f, 1);
}

#endregion

using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class Managers : MonoBehaviour
{
    public static Managers Instance { get; private set; }

    public static ResourceManager Resource { get; private set; }
    public static AudioManager Audio { get; private set; }
    public static UIManager UI { get; private set; }

    public static InputManager Input { get; private set; }
    public static TimeManager Time { get; private set; }
    public static PoolManager Pool { get; private set; }
    public static SceneLoader Scene { get; private set; }
    public static ItemManager Item { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async UniTask Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        DOTween.Init(true, true);

        Resource = new ResourceManager();
        Audio = new AudioManager();
        UI = new UIManager();
        Input = new InputManager();
        Time = new TimeManager();
        Pool = new PoolManager();
        Scene = new SceneLoader();
        Item = new ItemManager();

        await Resource.Init();
        await Audio.Init(transform);
        await UI.Init(transform);

        Input.Init();
        await Scene.Init();
        await Item.Init(Resource);
    }
}
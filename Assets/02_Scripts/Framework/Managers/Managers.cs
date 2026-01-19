using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Resources;
using Unity.VisualScripting;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class Managers : Singleton<Managers>
{
    public static readonly ResourceManager Resource = new();
    public static readonly PoolManager Pool = new();
    public static readonly SoundManager Sound = new();
    public static readonly UIManager UI = new();
    public static readonly InputManager Input = new();
    public static readonly DataManager Data = new();
    public static readonly AssetManager Asset = new();
    public static readonly SceneManager Scene = new();

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        Init().Forget();

        DebugManager.instance.enableRuntimeUI = false;
    }

    private async UniTask Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        DOTween.Init(true, true);

        await Resource.Init();
        await Sound.Init(transform);
        await UI.Init(transform);

        Input.Init();

        await Data.Init();
        await Asset.Init();
        await Scene.Init(transform);
    }
}
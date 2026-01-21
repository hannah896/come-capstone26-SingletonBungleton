using Cysharp.Threading.Tasks;
using UnityEngine;

public class Initializer : MonoBehaviour
{
    public static Initializer Instance { get; private set; }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        await InitializeAsync();
        await InitializeManagersAsync();
    }

    #region Create
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInitializer()
    {
        if (Object.FindAnyObjectByType<Initializer>() != null) return;
        GameObject obj = new GameObject("[Initializer]", typeof(Initializer));
        Object.DontDestroyOnLoad(obj);
    }

    public async UniTask InitializeManagersAsync()
    {
        var managersObj = Utility.GetOrCreateObjectOfType<Managers>();
        managersObj.name = "[Managers]";
        await managersObj.Init();

        managersObj.transform.SetAsFirstSibling();
        transform.SetSiblingIndex(1);
    }
    #endregion

    private async UniTask InitializeAsync()
    {
        await UniTask.CompletedTask;
    }
}
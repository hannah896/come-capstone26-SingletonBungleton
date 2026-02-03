using UnityEngine;

public class Initializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeManagersAsync()
    {
        Main main = Main.Instance;
        main.transform.SetAsFirstSibling();
    }
}
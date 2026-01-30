using UnityEngine;

public class Initializer
{
    /// <summary>
    /// 아무것도 없는 상태에서 실행하면 제일 먼저 실행되면서 Main 오브젝트를 생성한다.
    /// 씬이 로드가 다되기 전에 실행된다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeManagersAsync()
    {
        Main main = Main.Instance;
        main.transform.SetAsFirstSibling(); // 하이어라키 계층 최상단으로 옮긴다.
    }
}
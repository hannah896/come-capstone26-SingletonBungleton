using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 화면. 최종 보스를 쓰러뜨리면 검은 화면이 서서히 덮이며 엔딩 문구와 "로비로" 버튼이 나온다.
/// UI는 Resources/UI_Ending 프리팹에 있다 (에디터 도구로 만든 임시 화면 — 연출은 나중에 교체).
/// </summary>
public class EndingScreen : MonoBehaviour
{
    private const string ResourcePath = "UI_Ending";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button returnButton;
    [SerializeField] private float fadeSeconds = 2.5f;

    private static EndingScreen opened;
    private float elapsed;

    public static bool IsOpen => opened != null;

    /// <summary>엔딩 화면을 띄운다. 이미 떠 있으면 무시한다.</summary>
    public static void Show()
    {
        if (opened != null) return;

        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[Ending] Resources/{ResourcePath} 프리팹이 없습니다.");
            return;
        }

        GameObject go = Instantiate(prefab);
        opened = go.GetComponent<EndingScreen>();
        if (opened == null) opened = go.AddComponent<EndingScreen>();

        // 엔딩 중에는 게임 로직을 멈추고 커서를 풀어 버튼을 누를 수 있게 한다
        GameScene.GameState = GameState.Ending;
        GameScene.GameProcessing = GameProcessing.Stopping;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
        returnButton?.onClick.AddListener(() => ReturnToLobbyAsync().Forget());
    }

    private void OnDestroy()
    {
        if (opened == this) opened = null;
    }

    private void Update()
    {
        if (group == null) return;

        elapsed += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds));
    }

    // 방을 나가고 저장 세션을 끝낸 뒤 로비로 돌아간다 (GameScene의 불러오기 실패 복구와 같은 순서)
    private async UniTaskVoid ReturnToLobbyAsync()
    {
        if (returnButton != null) returnButton.interactable = false;

        if (Main.Network != null && Main.Network.IsInRoom)
            await Main.Network.LeaveRoomAsync();

        Main.Save.EndSession();
        await UniTask.WaitUntil(() => !Main.Scene.IsTransitioning);
        await Main.Scene.ChangeSceneAsync("LobbyScene");
    }
}

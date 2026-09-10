using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임 중 일시정지 팝업.
///
/// 버튼 4종
/// - Home    : 방을 떠나고 로비 씬으로
/// - Setting : 설정 팝업 열기 (이 팝업은 뒤에 남는다)
/// - Help    : 무한 낙하·지물 끼임 등 진행 불가 상태에서 스폰 지점으로 탈출
/// - Exit    : 프로그램 종료
///
/// 프리팹/씬 오브젝트 요구사항
/// - 자식 버튼 이름: UI_Button_Home / UI_Button_Setting / UI_Button_Help / UI_Button_Exit
///   ([SerializeField]로 직접 연결해도 되고, 비워두면 이름으로 찾는다)
/// - <see cref="Extensions.ShowPopup{T}"/>로 띄우려면 Addressable 키를 "UI_Popup_Pause"로 등록해야 한다.
/// </summary>
public class UI_Popup_Pause : UI_Popup
{
    #region Fields

    [Header("Buttons")]
    [SerializeField] private UI_Button UI_Button_Home;
    [SerializeField] private UI_Button UI_Button_Setting;
    [SerializeField] private UI_Button UI_Button_Help;
    [SerializeField] private UI_Button UI_Button_Exit;

    [Header("구조 버튼")]
    [Tooltip("구조 이동 후 이 팝업을 자동으로 닫는다")]
    [SerializeField] private bool closeAfterRescue = true;

    // 로비 이동·종료처럼 되돌릴 수 없는 동작이 버튼 연타로 중복 실행되는 것을 막는다.
    private bool _isHandled;

    // 팝업이 플레이어 입력을 차단했는지 여부 (닫을 때 복구용)
    private bool _playerInputBlocked;

    // 씬에 미리 배치된 인스턴스인지. true면 닫을 때 파괴하지 않고 비활성화만 한다.
    private bool _isScenePlaced;

    #endregion

    #region 열기 / 닫기 진입점

    // 사망 중 ESC를 눌렀을 때 안내 문구
    private const string DeadBlockedMessage = "부활한 뒤에 ESC를 누를 수 있습니다";
    private const float DeadBlockedToastDuration = 2f;

    // 지금 떠 있는 일시정지 팝업. 토글과 중복 열기 방지에 쓴다.
    private static UI_Popup_Pause s_current;

    /// <summary>현재 일시정지 팝업이 떠 있는지.</summary>
    public static bool IsOpen => s_current != null;

    /// <summary>떠 있으면 닫는다. (사망 진입 등 다른 UI가 화면을 차지해야 할 때)</summary>
    public static void CloseIfOpen()
    {
        if (s_current == null) return;
        s_current.Close();
    }

    /// <summary>
    /// ESC 토글 진입점. 떠 있으면 닫고, 없으면 연다.
    ///
    /// 사망 중에는 열지 않는다 — 사망 팝업과 겹쳐 두 UI가 동시에 뜨면
    /// 어느 버튼이 유효한지 알 수 없고, 부활/로비 버튼이 서로 충돌한다.
    /// 대신 부활 후에 쓸 수 있다는 토스트로 안내한다.
    /// </summary>
    public static void Toggle()
    {
        if (s_current != null)
        {
            s_current.Close();
            return;
        }

        Player player = FindLocalPlayer();
        if (player != null && player.IsDead)
        {
            Toast.Show(DeadBlockedMessage, DeadBlockedToastDuration, ToastColor.Yellow, ToastPosition.TopCenter);
            return;
        }

        OpenAsync().Forget();
    }

    private static async UniTaskVoid OpenAsync()
    {
        // 씬에 미리 배치해 둔 인스턴스가 있으면 그걸 쓴다.
        // (프리팹화 + Addressables 등록 전에도 동작하도록)
        UI_Popup_Pause placed = Object.FindFirstObjectByType<UI_Popup_Pause>(FindObjectsInactive.Include);
        if (placed != null)
        {
            s_current = placed;
            placed._isScenePlaced = true;
            placed.gameObject.SetActive(true);
            placed.Set();
            return;
        }

        var popup = await Extensions.ShowPopup<UI_Popup_Pause>(clickGuard: true);
        if (popup == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[UI_Popup_Pause] 팝업을 열지 못했습니다. " +
                             "Addressables에 'UI_Popup_Pause' 키가 등록됐는지 확인하세요.");
#endif
            return;
        }

        s_current = popup;
        popup.Set();
    }

    #endregion

    #region Initialize

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 프리팹에서 연결되지 않은 참조는 이름으로 보완한다.
        if (UI_Button_Home == null)
            UI_Button_Home = gameObject.FindChild<UI_Button>(nameof(UI_Button_Home));
        if (UI_Button_Setting == null)
            UI_Button_Setting = gameObject.FindChild<UI_Button>(nameof(UI_Button_Setting));
        if (UI_Button_Help == null)
            UI_Button_Help = gameObject.FindChild<UI_Button>(nameof(UI_Button_Help));
        if (UI_Button_Exit == null)
            UI_Button_Exit = gameObject.FindChild<UI_Button>(nameof(UI_Button_Exit));

        UI_Button_Home?.SetEvent(OnButtonHome);
        UI_Button_Setting?.SetEvent(OnButtonSetting);
        UI_Button_Help?.SetEvent(OnButtonHelp);
        UI_Button_Exit?.SetEvent(OnButtonExit);

        return true;
    }

    public void Set()
    {
        Initialize();

        // 씬 배치 인스턴스는 재사용되므로 열 때마다 상태를 되돌린다.
        _isHandled = false;

        // Start()는 최초 1회만 돌기 때문에, 재활성화 경로에서는 여기서 입력을 막는다.
        BlockPlayerInput();
    }

    protected override void Start()
    {
        base.Start();

        // 게임씬이면 플레이어 입력을 막는다 (팝업 뒤에서 계속 움직이지 않도록)
        BlockPlayerInput();
    }

    #endregion

    #region Close

    public override void Close()
    {
        if (_onClose) return;

        RestorePlayerInput();
        s_current = null;

        if (_isScenePlaced)
        {
            // 씬에 배치된 인스턴스는 UIManager가 모르는 오브젝트라 ClosePopup으로 정리되지 않는다.
            // 파괴하지 않고 감춰서 다음 ESC에 다시 쓴다.
            // (_onClose를 세우지 않아야 다시 열었을 때 또 닫을 수 있다)
            gameObject.SetActive(false);
            return;
        }

        base.Close();
    }

    #endregion

    #region Events

    // 로비로 — 세션에 참가 중이면 방을 떠난 뒤 씬을 전환한다. (UI_Popup_PlayerDie와 동일 경로)
    private void OnButtonHome()
    {
        if (_isHandled) return;
        _isHandled = true;

        // 팝업이 막아둔 입력을 되돌려놓고 나간다. 씬이 바뀌어도 InputManager는 유지되므로
        // 복구하지 않으면 다음 게임에서 플레이어 입력이 꺼진 채로 시작한다.
        RestorePlayerInput();

        // 다음 게임에 진행 상태가 남지 않도록 초기화한다.
        GameScene.GameState = GameState.None;
        GameScene.GameProcessing = GameProcessing.None;

        if (Main.Network != null && Main.Network.IsInRoom)
            Main.Network.LeaveRoomAsync().Forget();

        // UI/타이머/풀/에셋 정리는 SceneManagerEx의 씬 전환 표준 정리가 수행한다.
        Extensions.ChangeScene("LobbyScene");
    }

    // 설정 팝업 열기 — 이 팝업은 닫지 않고 뒤에 남긴다 (설정을 닫으면 다시 일시정지 화면).
    private void OnButtonSetting()
    {
        if (_isHandled) return;

        ShowSettingAsync().Forget();
    }

    private async UniTaskVoid ShowSettingAsync()
    {
        var popup = await Extensions.ShowPopup<UI_Popup_SettingUI>(clickGuard: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (popup == null)
            Debug.LogWarning("[UI_Popup_Pause] UI_Popup_SettingUI를 열지 못했습니다. " +
                             "Addressables에 'UI_Popup_SettingUI' 키가 등록됐는지 확인하세요.", this);
#endif
    }

    // 구조 — 무한 낙하·지물 끼임 등으로 진행할 수 없을 때 스폰 지점으로 옮긴다.
    private void OnButtonHelp()
    {
        if (_isHandled) return;

        Player player = FindLocalPlayer();
        if (player == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[UI_Popup_Pause] 로컬 플레이어를 찾지 못해 구조 이동을 건너뜁니다.", this);
#endif
            return;
        }

        if (!player.TeleportToSpawnPoint())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[UI_Popup_Pause] 스폰 지점을 알 수 없거나 사망 중이라 구조 이동에 실패했습니다.", this);
#endif
            return;
        }

        if (closeAfterRescue)
            Close();
    }

    // 프로그램 종료 — 세션에 참가 중이면 먼저 방을 떠난다.
    private void OnButtonExit()
    {
        if (_isHandled) return;
        _isHandled = true;

        Quit();
    }

    private void Quit()
    {
        // 퇴장 요청만 던지고 기다리지 않는다.
        //
        // LeaveRoomAsync는 Fusion 러너를 셧다운하는데, 러너가 내려가면 이 피어의 NetworkObject가
        // 전부 디스폰된다 — 호스트가 스폰해 준 내 캐릭터도 같이 사라진다.
        // 지형은 피어마다 로컬 생성이라 그대로 남으므로, 캐릭터만 없어진 정지 화면이 보인다.
        // (시네머신 카메라도 Follow 대상을 잃고 그 자리에 굳는다)
        // 그 상태로 셧다운 완료를 기다리면 그 정지 화면이 그대로 노출되므로,
        // 요청만 보내고 곧바로 종료한다. 종료 과정에서 소켓이 닫히면 호스트도 이탈을 인지한다.
        if (Main.Network != null && Main.Network.IsInRoom)
            Main.Network.LeaveRoomAsync().Forget();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Helpers

    // 이 피어가 조작하는 캐릭터를 찾는다. 멀티에서는 남의 캐릭터도 씬에 있으므로 IsLocalPlayer로 거른다.
    private static Player FindLocalPlayer()
    {
        Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsLocalPlayer)
                return players[i];
        }

        return null;
    }

    #endregion

    #region Input Block

    // 게임씬에서만 플레이어 입력을 차단한다. (UI_Popup_SettingUI와 동일 규약)
    private void BlockPlayerInput()
    {
        if (Main.Scene?.Current is not GameScene) return;
        if (Main.Input == null) return;
        if (!Main.Input.IsActive<InputActions_PlayerInputHandler>()) return;

        Main.Input.RemoveInput<InputActions_PlayerInputHandler>();
        _playerInputBlocked = true;
    }

    // 차단했던 플레이어 입력을 복구한다.
    private void RestorePlayerInput()
    {
        if (!_playerInputBlocked) return;
        _playerInputBlocked = false;

        if (Main.Input == null) return;

        Main.Input.AddInput<InputActions_PlayerInputHandler>();
    }

    #endregion
}

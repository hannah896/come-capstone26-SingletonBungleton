using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 방 만들기 팝업. 방 이름과 최대 인원을 입력받아 호스트로 방을 생성하고 게임씬으로 전환한다.
/// 호스트가 월드 시드/옵션을 확정해 세션에 공유하므로, 참가자들이 동일 월드를 생성한다.
/// 버튼 처리는 UI_HUD_LobbyScene과 동일하게 SetDownUpButton + OnButtonUp 방식을 사용.
/// </summary>
public class UI_Popup_MakeRoom : UI_Popup
{
    #region Fields
    [Header("방 이름")]
    [SerializeField] private TMP_InputField RoomNameInput;

    [Header("최대 인원 버튼 (인덱스 순서 = 1~4명)")]
    [SerializeField] private UI_Button[] MaxPlayerButtons;

    [Header("하단 버튼")]
    [SerializeField] private UI_Button MakeRoomButton;
    [SerializeField] private UI_Button CloseButton;

    // 선택 표시용 색 (선택 버튼만 이미지 알파를 켜고, 미선택 버튼은 알파 0으로 꺼둠)
    private static readonly Color SelectedColor = Color.white;
    private static readonly Color UnselectedColor = new(1f, 1f, 1f, 0f);

    // 선택된 최대 인원 (기본 4명)
    private int _maxPlayers = 4;
    #endregion

    protected override void Start()
    {
        base.Start();

        // 최대 인원 버튼: 인덱스 i → 인원 (i + 1)명
        for (int i = 0; i < MaxPlayerButtons.Length; i++)
        {
            UI_Button button = MaxPlayerButtons[i];
            if (button == null) continue;

            int count = i + 1; // 클로저 캡처 방지용 지역 변수
            button.SetDownUpButton();
            button.OnButtonUp += () => OnSelectMaxPlayers(count);
        }

        if (MakeRoomButton != null)
        {
            MakeRoomButton.SetDownUpButton();
            MakeRoomButton.OnButtonUp += OnMakeRoom;
        }

        if (CloseButton != null)
        {
            CloseButton.SetDownUpButton();
            CloseButton.OnButtonUp += OnClose;
        }

        RefreshMaxPlayerButtons();
    }

    // 최대 인원 선택
    private void OnSelectMaxPlayers(int count)
    {
        _maxPlayers = count;
        RefreshMaxPlayerButtons();
    }

    // 선택된 인원 버튼만 밝게 표시
    private void RefreshMaxPlayerButtons()
    {
        for (int i = 0; i < MaxPlayerButtons.Length; i++)
        {
            if (MaxPlayerButtons[i] == null) continue;
            MaxPlayerButtons[i].SetColor(i + 1 == _maxPlayers ? SelectedColor : UnselectedColor);
        }
    }

    // 방 생성 (호스트) → 시드/옵션 세션 공유 → 게임씬 진입
    private void OnMakeRoom()
    {
        MakeRoomAsync().Forget();
    }

    private async UniTaskVoid MakeRoomAsync()
    {
        string roomName = RoomNameInput != null && !string.IsNullOrWhiteSpace(RoomNameInput.text)
            ? RoomNameInput.text
            : "TestRoom";

        // 호스트가 월드 시드/옵션을 확정 (마일스톤1: 옵션은 기본값, 시드만 랜덤)
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        WorldBranchSetting branch = WorldBranchSetting.Default;
        WorldLoopSetting loop = WorldLoopSetting.Default;
        WorldSize size = WorldSize.Large;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[UI_Popup_MakeRoom] 방 생성: {roomName} (최대 {_maxPlayers}명, seed {seed})");
#endif

        // 방 생성 + 옵션을 세션 속성으로 공유
        var props = NetworkWorldConfig.ToProperties(branch, loop, seed, size);
        bool ok = await Main.Network.HostRoomAsync(roomName, _maxPlayers, props);
        if (!ok)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[UI_Popup_MakeRoom] 방 생성 실패: {roomName}");
#endif
            return;
        }

        // 호스트도 로컬에 동일 옵션 반영 후 게임씬 진입
        WorldGenRequest.Set(branch, loop, seed, size);

        Close();
        Extensions.ChangeScene("GameScene");
    }

    private void OnClose()
    {
        Close();
    }
}

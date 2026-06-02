using Cysharp.Threading.Tasks;
using UnityEngine;

public class UI_HUD_LobbyScene : UI_Hud
{
    [SerializeField] private UI_Button UI_Button_MakeRoom;
    [SerializeField] private UI_Button UI_Button_EnterRoom;
    [SerializeField] private UI_Button UI_Button_Exit;

    protected override void Start()
    {
        base.Start();

        // OnButtonUp은 SetDownUpButton()으로 PointerUp EventTrigger를 등록해야 발생한다.
        UI_Button_MakeRoom.SetDownUpButton();
        UI_Button_MakeRoom.OnButtonUp += OnMakeRoom;

        UI_Button_EnterRoom.SetDownUpButton();
        UI_Button_EnterRoom.OnButtonUp += OnEnterRoom;

        UI_Button_Exit.SetDownUpButton();
        UI_Button_Exit.OnButtonUp += OnExit;
    }

    private void OnMakeRoom()
    {
        // TODO: 나중엔 방 데이터 UI를 띄우는 흐름으로 교체.
        // 지금은 바로 게임씬으로 전환 — ChangeScene이 UI_Screen_Transition(전환 오버레이)을
        // 자동으로 띄워 로딩 화면 역할을 하고, GameScene EnterScene이 월드 생성을 끝낸 뒤 닫힌다.
        // (WorldGenRequest가 없으면 GameScene이 기본 옵션으로 월드를 생성)
        Extensions.ChangeScene("GameScene");
    }

    private void OnEnterRoom() 
    {

    }

    private void OnExit()
    {
        Application.Quit();
    }
}
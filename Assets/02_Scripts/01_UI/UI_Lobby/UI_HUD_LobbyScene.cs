using Cysharp.Threading.Tasks;
using UnityEngine;

public class UI_HUD_LobbyScene : UI_Hud
{
    [SerializeField] private UI_Button UI_Button_MakeRoom;
    [SerializeField] private UI_Button UI_Button_EnterRoom;
    [SerializeField] private UI_Button UI_Button_Setting;
    [SerializeField] private UI_Button UI_Button_Exit;


    protected override void Start()
    {
        base.Start();

        // OnButtonUp은 SetDownUpButton()으로 PointerUp EventTrigger를 등록해야 발생한다.
        UI_Button_MakeRoom.SetDownUpButton();
        UI_Button_MakeRoom.OnButtonUp += OnMakeRoom;

        UI_Button_EnterRoom.SetDownUpButton();
        UI_Button_EnterRoom.OnButtonUp += OnEnterRoom;

        UI_Button_Setting.SetDownUpButton();
        UI_Button_Setting.OnButtonUp += OnSetting;

        UI_Button_Exit.SetDownUpButton();
        UI_Button_Exit.OnButtonUp += OnExit;
    }

    private void OnMakeRoom()
    {
        // 방 만들기 팝업을 띄운다. 방 이름/최대 인원 선택 후 팝업에서 게임씬으로 전환한다.
        Extensions.ShowPopup<UI_Popup_MakeRoom>(clickGuard: true).Forget();
    }

    private void OnEnterRoom()
    {
        // 방 입장 팝업을 띄운다. 방 목록에서 선택 후 팝업에서 게임씬으로 전환한다.
        Extensions.ShowPopup<UI_Popup_EnterRoom>(clickGuard: true, clickClose: true).Forget();
    }

    private void OnSetting() 
    {
        Extensions.ShowPopup<UI_Popup_SettingUI>().Forget();
    }


    private void OnExit()
    {
#if UNITY_EDITOR
        // 에디터 플레이 모드에서는 Application.Quit()이 동작하지 않으므로 플레이 모드를 종료한다.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 플레이어 사망 시 표시되는 게임 오버 팝업.
/// <see cref="PlayerDeadState"/>가 사망 연출이 끝난 뒤 띄우고 <see cref="Set"/>으로 대상을 주입한다.
///
/// 프리팹 요구사항 (Addressable 키 = "UI_Popup_PlayerDie")
/// - 자식 오브젝트 이름을 필드명과 동일하게 두면 UI_Panel.OnValidate가 자동 연결한다.
///   txtTitle / txtCause / txtSurvivedDay (UI_Text), btnRespawn / btnLobby (UI_Button)
/// </summary>
public class UI_Popup_PlayerDie : UI_Popup
{
    #region Fields

    [Header("Texts")]
    [SerializeField] private UI_Text txtTitle;
    [SerializeField] private UI_Text txtCause;
    [SerializeField] private UI_Text txtSurvivedDay;

    [Header("Buttons")]
    [SerializeField] private UI_Button btnRespawn;
    [SerializeField] private UI_Button btnLobby;

    // 사망한 로컬 플레이어. 부활 버튼의 대상이며, 없으면 부활 버튼을 숨긴다.
    private Player _player;

    // 버튼 연타로 부활과 씬 전환이 동시에 실행되는 것을 막는다.
    private bool _isHandled;

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 프리팹에서 [SerializeField]로 연결되지 않은 참조는 이름으로 보완한다.
        if (txtTitle == null) txtTitle = gameObject.FindChild<UI_Text>(nameof(txtTitle));
        if (txtCause == null) txtCause = gameObject.FindChild<UI_Text>(nameof(txtCause));
        if (txtSurvivedDay == null) txtSurvivedDay = gameObject.FindChild<UI_Text>(nameof(txtSurvivedDay));
        if (btnRespawn == null) btnRespawn = gameObject.FindChild<UI_Button>(nameof(btnRespawn));
        if (btnLobby == null) btnLobby = gameObject.FindChild<UI_Button>(nameof(btnLobby));

        btnRespawn?.SetEvent(OnButtonRespawn);
        btnLobby?.SetEvent(OnButtonLobby);

        return true;
    }

    /// <summary>
    /// 사망한 플레이어와 사망 종류를 주입한다.
    /// </summary>
    /// <param name="player">부활 대상. null이면 부활 버튼을 숨긴다.</param>
    /// <param name="wasHit">true면 피격사, false면 자연사(허기·Ego)</param>
    public void Set(Player player, bool wasHit)
    {
        Initialize();

        _player = player;

        string cause = wasHit ? "적에게 당했습니다" : "굶주림을 이기지 못했습니다";

        // WorldClock이 없는 테스트 씬에서는 생존 일수를 비워 둔다.
        int day = WorldClock.Instance != null ? WorldClock.Instance.CurrentDay : 0;
        string survived = day > 0 ? $"{day}일째 생존" : string.Empty;

        if (txtCause != null) txtCause.Text = cause;
        if (txtSurvivedDay != null) txtSurvivedDay.Text = survived;

        // 프리팹에 텍스트가 하나뿐이면 내용을 타이틀 한 곳에 모아 보여준다.
        if (txtTitle != null)
            txtTitle.Text = BuildTitleText(cause, survived);

        if (btnRespawn != null)
            btnRespawn.gameObject.SetActive(_player != null);
    }

    private string BuildTitleText(string cause, string survived)
    {
        string text = "사 망";

        if (txtCause == null && !string.IsNullOrEmpty(cause))
            text += $"\n{cause}";

        if (txtSurvivedDay == null && !string.IsNullOrEmpty(survived))
            text += $"\n{survived}";

        return text;
    }

    #endregion

    #region Events

    // 부활 — 스탯 회복·리스폰 지점 이동·Locomotion 복귀는 Player.Revive()가 수행한다.
    // 부활에 성공하면 상태 전환으로 PlayerDeadState.OnExit이 이 팝업을 닫으므로 여기서 닫지 않는다.
    // (양쪽에서 닫으면 파괴 중인 오브젝트에 닫기 애니메이션이 한 번 더 걸린다)
    private void OnButtonRespawn()
    {
        if (_isHandled) return;
        _isHandled = true;

        // 이미 사망 상태가 아니면(중복 처리 등) 팝업만 정리한다.
        if (_player == null || !_player.Revive())
            Close();
    }

    // 로비로 나가기 — 세션에 참가 중이면 방을 떠난 뒤 씬을 전환한다.
    private void OnButtonLobby()
    {
        if (_isHandled) return;
        _isHandled = true;

        // 다음 게임에서 사망 상태가 남지 않도록 정적 진행 상태를 초기화한다.
        GameScene.GameState = GameState.None;
        GameScene.GameProcessing = GameProcessing.None;

        if (Main.Network != null && Main.Network.IsInRoom)
            Main.Network.LeaveRoomAsync().Forget();

        // UI/타이머/풀/에셋 정리는 SceneManagerEx의 씬 전환 표준 정리가 수행한다.
        Extensions.ChangeScene("LobbyScene");
    }

    #endregion
}

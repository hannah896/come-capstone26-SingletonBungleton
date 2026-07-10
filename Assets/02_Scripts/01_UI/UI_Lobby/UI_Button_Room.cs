using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 방 입장 팝업(UI_Popup_EnterRoom)의 방 목록 개별 항목.
/// 방 이름과 인원(현재/최대)을 표시하고, 클릭 시 자기 자신을 전달하는 선택 이벤트를 제공한다.
/// UI_Button_Room.prefab 루트에 컴포넌트로 추가만 하면, Button / 텍스트는 미연결 시 자동 탐색된다.
/// </summary>
public class UI_Button_Room : MonoBehaviour
{
    #region Fields
    [Header("연결 (비우면 자동 탐색)")]
    [SerializeField] private UI_Button Button;          // 미연결 시 루트에서 자동 획득
    [SerializeField] private TMP_Text RoomNameText;     // 미연결 시 자식 텍스트 중 첫 번째
    [SerializeField] private TMP_Text PlayerCountText;  // 미연결 시 자식 텍스트 중 두 번째

    // 선택 표시용 색
    private static readonly Color NormalColor = Color.white;
    private static readonly Color SelectedColor = new(0.6f, 0.85f, 1f, 1f);

    private bool _resolved;
    #endregion

    #region Properties
    public string RoomName { get; private set; }
    public int CurrentPlayers { get; private set; }
    public int MaxPlayers { get; private set; }

    // 정원 미달이면 입장 가능
    public bool IsJoinable => CurrentPlayers < MaxPlayers;
    #endregion

    // 항목 클릭 이벤트 (자기 자신 전달)
    public event Action<UI_Button_Room> OnClicked;

    // Button / 텍스트 참조를 확보(미연결 시 자동 탐색)
    private void ResolveReferences()
    {
        if (_resolved) return;
        _resolved = true;

        if (Button == null) Button = GetComponent<UI_Button>();

        if (RoomNameText == null || PlayerCountText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            if (RoomNameText == null && texts.Length > 0) RoomNameText = texts[0];
            if (PlayerCountText == null && texts.Length > 1) PlayerCountText = texts[1];
        }
    }

    // 방 정보 세팅
    public void SetInfo(string roomName, int current, int max)
    {
        ResolveReferences();

        RoomName = roomName;
        CurrentPlayers = current;
        MaxPlayers = max;

        if (RoomNameText != null) RoomNameText.text = roomName;
        if (PlayerCountText != null) PlayerCountText.text = $"{current}/{max}";
    }

    // 스폰 직후 팝업에서 1회 호출 — 버튼 이벤트 연결
    public void Bind()
    {
        ResolveReferences();
        if (Button == null) return;

        // OnButtonUp은 SetDownUpButton()으로 PointerUp EventTrigger를 등록해야 발생한다.
        Button.SetDownUpButton();
        Button.OnButtonUp += () => OnClicked?.Invoke(this);
    }

    // 선택 여부에 따라 색 갱신
    public void SetSelected(bool selected)
    {
        ResolveReferences();
        if (Button != null) Button.SetColor(selected ? SelectedColor : NormalColor);
    }
}

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 입장 팝업. 실제 방 목록을 조회해 표시하고, 클릭한 방으로 참가한다.
///
/// 방 목록은 NetworkManager를 통해 얻는다:
///   - 열릴 때 Main.Network.BrowseRoomsAsync()로 로비 접속(방 탐색 시작)
///   - Main.Network.OnRoomListUpdated 이벤트로 방 목록을 받아 갱신
///   - 방 클릭 시 Main.Network.JoinRoomByNameAsync()로 참가 후 게임씬 전환
///
/// 목록 항목은 프리팹 계층(ScrollView/Content)에 배치된 방 버튼 오브젝트를
/// "템플릿"으로 캐싱해두고 복제하며, 복제본에 GetOrAddComponent로 UI_Button_Room을 붙인다.
/// </summary>
public class UI_Popup_EnterRoom : UI_Popup
{
    #region Fields
    [Header("연결 (비우면 자동 탐색)")]
    [SerializeField] private GameObject RoomButtonTemplate; // 목록 항목 템플릿(방 버튼 오브젝트)
    [SerializeField] private Transform RoomListContainer;   // 비우면 ScrollRect.content 자동
    [SerializeField] private GameObject EmptyText;          // 비우면 이름에 "Empty" 포함된 오브젝트 자동
    [SerializeField] private UI_Button RefreshButton;       // 비우면 이름에 "Refresh" 포함된 버튼 자동
    [SerializeField] private UI_Button CloseButton;         // 비우면 이름에 "Close" 포함된 버튼 자동

    // 실제 사용하는 템플릿 (RoomButtonTemplate 연결 시 그것을, 아니면 자동 탐색 결과를 캐싱)
    private GameObject _template;

    // 생성된 방 항목들
    private readonly List<UI_Button_Room> _roomButtons = new();

    private bool _cached;

    // 새로고침 진행 중 여부 (중복 클릭 방지)
    private bool _refreshing;
    #endregion

    protected override void Start()
    {
        base.Start();

        CacheReferences();
        BindButtons();

        // 방 목록 갱신 구독. 해제는 파괴 시점(OnDestroyEvent)에 수행해
        // 베이스 UI_Popup.OnDestroy(트윈 정리 등)를 덮어쓰지 않는다.
        if (Main.Network != null)
        {
            Main.Network.OnRoomListUpdated += OnRoomListUpdated;
            OnDestroyEvent.AddListener(Unsubscribe);
        }

        // 마지막으로 받아둔 목록을 먼저 그린다.
        // (이미 로비에 접속된 상태로 팝업을 다시 열면 Fusion이 새 갱신을 보내주지 않아 빈 화면이 된다)
        RebuildRooms(Main.Network?.CachedRooms);
        Main.Network?.BrowseRoomsAsync().Forget();
    }

    // 새로고침 / 닫기 버튼 연결
    private void BindButtons()
    {
        // OnButtonUp은 SetDownUpButton()으로 PointerUp EventTrigger를 등록해야 발생한다.
        if (RefreshButton != null)
        {
            RefreshButton.SetDownUpButton();
            RefreshButton.OnButtonUp += OnRefresh;
        }

        if (CloseButton != null)
        {
            CloseButton.SetDownUpButton();
            CloseButton.OnButtonUp += OnCloseButton;
        }
    }

    // 새로고침 — 로비에 재접속해 방 목록 전체를 다시 받아온다
    private void OnRefresh()
    {
        RefreshAsync().Forget();
    }

    private async UniTaskVoid RefreshAsync()
    {
        if (_refreshing || Main.Network == null) return;

        _refreshing = true;
        RefreshButton?.SetActive(false); // 갱신 중 중복 클릭 차단 (버튼 흐리게)

        try
        {
            await Main.Network.RefreshRoomsAsync();
        }
        finally
        {
            _refreshing = false;

            // 갱신 도중 팝업이 닫혀 파괴됐을 수 있다
            if (this != null && RefreshButton != null) RefreshButton.SetActive(true);
        }
    }

    // 닫기 버튼
    private void OnCloseButton()
    {
        Close();
    }

    // 방 목록 이벤트 해제
    private void Unsubscribe()
    {
        if (Main.Instance != null && Main.Network != null)
            Main.Network.OnRoomListUpdated -= OnRoomListUpdated;
    }

    // 템플릿/컨테이너/빈 안내 텍스트를 1회 캐싱
    private void CacheReferences()
    {
        if (_cached) return;
        _cached = true;

        // 컨테이너: ScrollRect.content 우선
        if (RoomListContainer == null)
        {
            ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll != null && scroll.content != null) RoomListContainer = scroll.content;
        }

        // 템플릿: Inspector에서 연결한 오브젝트 우선, 없으면 컨테이너 안에서 자동 탐색
        // (자동 탐색은 UI_Button_Room이 붙어 있으면 그것을, 없으면 UI_Button 오브젝트를 사용)
        if (RoomButtonTemplate != null)
        {
            _template = RoomButtonTemplate;
        }
        else
        {
            Transform searchRoot = RoomListContainer != null ? RoomListContainer : transform;

            UI_Button_Room existing = searchRoot.GetComponentInChildren<UI_Button_Room>(true);
            if (existing != null)
            {
                _template = existing.gameObject;
            }
            else
            {
                UI_Button button = searchRoot.GetComponentInChildren<UI_Button>(true);
                if (button != null) _template = button.gameObject;
            }
        }

        if (RoomListContainer == null && _template != null)
            RoomListContainer = _template.transform.parent;

        if (EmptyText == null) EmptyText = FindEmptyText();

        if (RefreshButton == null) RefreshButton = FindButton("Refresh");
        if (CloseButton == null) CloseButton = FindButton("Close");
    }

    // 이름에 키워드가 포함된 UI_Button을 찾는다.
    // 방 목록 컨테이너 안쪽(= 방 항목 템플릿과 그 복제본)은 제외한다.
    private UI_Button FindButton(string keyword)
    {
        foreach (UI_Button button in GetComponentsInChildren<UI_Button>(true))
        {
            if (button.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (RoomListContainer != null && button.transform.IsChildOf(RoomListContainer)) continue;
            return button;
        }
        return null;
    }

    // 방 목록 수신 → 갱신
    private void OnRoomListUpdated(List<RoomInfo> rooms)
    {
        RebuildRooms(rooms);
    }

    // 방 목록을 화면에 다시 그린다
    private void RebuildRooms(IReadOnlyList<RoomInfo> rooms)
    {
        ClearRoomList();

        if (_template == null || RoomListContainer == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[UI_Popup_EnterRoom] 방 버튼 템플릿 또는 컨테이너를 찾지 못했습니다. " +
                             "ScrollView/Content 안에 방 버튼 오브젝트가 있는지 확인하세요.");
#endif
            SetEmptyTextActive(true);
            return;
        }

        // 원본은 템플릿으로만 사용 (화면에는 복제본만 표시)
        _template.SetActive(false);

        int count = rooms?.Count ?? 0;
        SetEmptyTextActive(count == 0);

        for (int i = 0; i < count; i++)
        {
            RoomInfo room = rooms[i];

            GameObject go = Instantiate(_template, RoomListContainer);
            go.SetActive(true);

            // 복제본에 컴포넌트 확보(프리팹에 미리 안 붙어 있어도 런타임에 부착)
            UI_Button_Room button = go.GetOrAddComponent<UI_Button_Room>();
            button.SetInfo(room.Name, room.PlayerCount, room.MaxPlayers);
            button.Bind();
            button.OnClicked += OnClickRoom;
            _roomButtons.Add(button);
        }
    }

    // 방 클릭 → 참가 (정원 미달인 방만)
    private void OnClickRoom(UI_Button_Room room)
    {
        if (room == null || !room.IsJoinable) return;
        JoinRoomAsync(room.RoomName).Forget();
    }

    private async UniTaskVoid JoinRoomAsync(string roomName)
    {
        if (Main.Network == null) return;

        bool ok = await Main.Network.JoinRoomByNameAsync(roomName);
        if (!ok)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[UI_Popup_EnterRoom] 방 참가 실패: {roomName}");
#endif
            return;
        }

        // 호스트가 세션에 공유한 월드 시드/옵션을 읽어 로컬 WorldGenRequest에 반영
        // (같은 시드로 각 클라가 동일 월드를 생성)
        NetworkWorldConfig.ApplyFromSession();

        Close();
        Extensions.ChangeScene("GameScene");
    }

    // 생성된 방 항목 정리
    private void ClearRoomList()
    {
        foreach (UI_Button_Room button in _roomButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }

        _roomButtons.Clear();
    }

    // 이름에 "Empty"가 포함된 자식을 빈 안내 텍스트로 사용
    private GameObject FindEmptyText()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            if (t.name.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0)
                return t.gameObject;
        }
        return null;
    }

    private void SetEmptyTextActive(bool active)
    {
        if (EmptyText != null) EmptyText.SetActive(active);
    }
}

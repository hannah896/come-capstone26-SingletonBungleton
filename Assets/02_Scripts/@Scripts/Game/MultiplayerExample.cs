using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if PHOTON_FUSION
using Fusion;
#endif

/// <summary>
/// 멀티플레이어 흐름 예시 스크립트.
/// NetworkManager, CommandManager, SimulationManager의 연동 방법을 보여줍니다.
/// 테스트용이므로 실제 게임에서는 적절한 UI/씬 구조로 분리해야 합니다.
/// </summary>
public class MultiplayerExample : MonoBehaviour
{
    #region Fields

    [Header("네트워크 설정")]
    [SerializeField] private string _roomName = "TestRoom"; // 방 이름
    [SerializeField] private int _maxPlayers = 4; // 최대 플레이어 수
    [SerializeField] private int _tickRate = 20; // 초당 시뮬레이션 틱 수

    // 예시 명령 실행기
    private SampleCommandExecutor _executor;

    // UI 상태
    private string _log = "";
    private Vector2 _scrollPos;

    #endregion

    #region Lifecycle

    private void Start()
    {
        SetupSimulation();
    }

    private void OnDestroy()
    {
        CleanupSimulation();
    }

    #endregion

    #region Simulation 설정

    // 시뮬레이션과 명령 실행기를 초기화
    private void SetupSimulation()
    {
        // 1. 틱 레이트 설정
        Main.Simulation.SetTickRate(_tickRate);

        // 2. 명령 실행기 등록
        _executor = new SampleCommandExecutor();
        Main.Simulation.RegisterExecutor(_executor,
            CommandType.Move,
            CommandType.Stop,
            CommandType.Attack,
            CommandType.Build,
            CommandType.Train);

        // 3. 명령 이벤트 구독 (디버그/UI 갱신용)
        Main.Command.OnCommandCreated += OnCommandCreated;
        Main.Command.OnCommandExecuted += OnCommandExecuted;

        // 4. 틱 이벤트 구독 (게임 로직 업데이트용)
        Main.Simulation.OnTick += OnSimulationTick;

        Log("[Setup] 시뮬레이션 초기화 완료");
    }

    // 정리
    private void CleanupSimulation()
    {
        Main.Command.OnCommandCreated -= OnCommandCreated;
        Main.Command.OnCommandExecuted -= OnCommandExecuted;
        Main.Simulation.OnTick -= OnSimulationTick;
    }

    #endregion

    #region 싱글플레이어 시작

    /// <summary>
    /// 싱글플레이어 모드로 게임을 시작합니다.
    /// 네트워크 없이 CommandManager → SimulationManager 직통 경로를 사용합니다.
    /// </summary>
    private void StartSinglePlayer()
    {
        // 싱글플레이어 모드 설정
        Main.Command.SetMultiplayerMode(false);

        // 시뮬레이션 시작
        Main.Simulation.StartSimulation();

        Log("[SinglePlayer] 게임 시작");
    }

    #endregion

    #region 멀티플레이어 시작

    /// <summary>
    /// 멀티플레이어 모드로 방을 생성합니다 (Host).
    /// </summary>
    private void CreateRoom()
    {
#if PHOTON_FUSION
        CreateRoomAsync().Forget();
#else
        Log("[Error] PHOTON_FUSION이 정의되지 않았습니다. Scripting Define Symbols에 추가하세요.");
#endif
    }

    /// <summary>
    /// 멀티플레이어 모드로 방에 참가합니다 (Client).
    /// </summary>
    private void JoinRoom()
    {
#if PHOTON_FUSION
        JoinRoomAsync().Forget();
#else
        Log("[Error] PHOTON_FUSION이 정의되지 않았습니다. Scripting Define Symbols에 추가하세요.");
#endif
    }

#if PHOTON_FUSION

    private async UniTaskVoid CreateRoomAsync()
    {
        Log("[Network] 로비 접속 중...");

        // 1. 로비 접속
        var lobbyResult = await Main.Network.ConnectToLobbyAsync();
        if (!lobbyResult)
        {
            Log("[Network] 로비 접속 실패");
            return;
        }

        Log("[Network] 로비 접속 완료. 방 생성 중...");

        // 2. 방 생성 (Host)
        var roomResult = await Main.Network.CreateRoomAsync(new RoomCreateArgs(_roomName, _maxPlayers));
        if (!roomResult)
        {
            Log("[Network] 방 생성 실패");
            return;
        }

        Log($"[Network] 방 생성 완료: {_roomName}");

        // 3. 멀티플레이어 모드 설정
        var localPlayerId = (ushort)Main.Network.LocalPlayer.PlayerId;
        Main.Command.SetMultiplayerMode(true, localPlayerId);

        // 4. 네트워크 이벤트 구독
        SubscribeNetworkEvents();

        // 5. 시뮬레이션 시작
        Main.Simulation.StartSimulation();

        Log("[Game] 멀티플레이어 게임 시작 (Host)");
    }

    private async UniTaskVoid JoinRoomAsync()
    {
        Log("[Network] 로비 접속 중...");

        // 1. 로비 접속
        var lobbyResult = await Main.Network.ConnectToLobbyAsync();
        if (!lobbyResult)
        {
            Log("[Network] 로비 접속 실패");
            return;
        }

        Log($"[Network] 방 참가 중: {_roomName}");

        // 2. 방 참가 (Client)
        var roomResult = await Main.Network.JoinRoomAsync(new RoomJoinArgs(_roomName));
        if (!roomResult)
        {
            Log("[Network] 방 참가 실패");
            return;
        }

        Log($"[Network] 방 참가 완료: {_roomName}");

        // 3. 멀티플레이어 모드 설정
        var localPlayerId = (ushort)Main.Network.LocalPlayer.PlayerId;
        Main.Command.SetMultiplayerMode(true, localPlayerId);

        // 4. 네트워크 이벤트 구독
        SubscribeNetworkEvents();

        // 5. 시뮬레이션 시작 (호스트의 틱에 동기화)
        Main.Simulation.StartSimulation();

        Log("[Game] 멀티플레이어 게임 시작 (Client)");
    }

    // 네트워크 이벤트 구독
    private void SubscribeNetworkEvents()
    {
        Main.Network.OnPlayerJoinedEvent += OnPlayerJoined;
        Main.Network.OnPlayerLeftEvent += OnPlayerLeft;
        Main.Network.OnStateChanged += OnNetworkStateChanged;
    }

    // 네트워크 이벤트 해제
    private void UnsubscribeNetworkEvents()
    {
        Main.Network.OnPlayerJoinedEvent -= OnPlayerJoined;
        Main.Network.OnPlayerLeftEvent -= OnPlayerLeft;
        Main.Network.OnStateChanged -= OnNetworkStateChanged;
    }

    private void OnPlayerJoined(PlayerRef player, NetworkPlayerData data)
    {
        Log($"[Network] 플레이어 입장: {player} (현재 {Main.Network.PlayerCount}명)");
    }

    private void OnPlayerLeft(PlayerRef player)
    {
        Log($"[Network] 플레이어 퇴장: {player} (현재 {Main.Network.PlayerCount}명)");
    }

    private void OnNetworkStateChanged(NetworkState state)
    {
        Log($"[Network] 상태 변경: {state}");
    }

#endif

    #endregion

    #region 게임 종료

    /// <summary>
    /// 게임을 종료하고 네트워크 연결을 해제합니다.
    /// </summary>
    private void StopGame()
    {
#if PHOTON_FUSION
        StopGameAsync().Forget();
#else
        Main.Simulation.StopSimulation();
        Log("[Game] 게임 종료");
#endif
    }

#if PHOTON_FUSION

    private async UniTaskVoid StopGameAsync()
    {
        // 시뮬레이션 정지
        Main.Simulation.StopSimulation();

        // 네트워크 이벤트 해제
        UnsubscribeNetworkEvents();

        // 방 퇴장
        if (Main.Network.IsConnected)
        {
            await Main.Network.LeaveRoomAsync();
        }

        // 싱글 모드로 복귀
        Main.Command.SetMultiplayerMode(false);

        Log("[Game] 게임 종료 및 연결 해제");
    }

#endif

    #endregion

    #region 명령 생성 예시

    // 이동 명령 예시
    private void SendMoveCommand(Vector2 targetPos)
    {
        // 선택된 유닛 ID들 (실제로는 SelectionManager 등에서 가져옴)
        int[] selectedUnitIds = { 1, 2, 3 };

        Main.Command.CreateMoveCommand(targetPos, queued: false, selectedUnitIds);
        Log($"[Command] 이동 명령: ({targetPos.x:F1}, {targetPos.y:F1})");
    }

    // 공격 명령 예시
    private void SendAttackCommand(int targetId)
    {
        int[] selectedUnitIds = { 1, 2, 3 };

        Main.Command.CreateAttackCommand(targetId, queued: false, selectedUnitIds);
        Log($"[Command] 공격 명령: target={targetId}");
    }

    // 건설 명령 예시
    private void SendBuildCommand(int buildingTypeId, Vector2 position)
    {
        int builderId = 1; // 건설 유닛 ID

        Main.Command.CreateBuildCommand(buildingTypeId, position, builderId);
        Log($"[Command] 건설 명령: type={buildingTypeId}, pos=({position.x:F1}, {position.y:F1})");
    }

    // 유닛 생산 명령 예시
    private void SendTrainCommand(int unitTypeId, int buildingId)
    {
        Main.Command.CreateTrainCommand(unitTypeId, buildingId);
        Log($"[Command] 생산 명령: unitType={unitTypeId}, building={buildingId}");
    }

    #endregion

    #region 이벤트 콜백

    // 명령 생성 시
    private void OnCommandCreated(GameCommand command)
    {
        Log($"  [Created] {command.Type} at tick {command.Tick}");
    }

    // 명령 실행 완료 시
    private void OnCommandExecuted(GameCommand command)
    {
        Log($"  [Executed] {command.Type} at tick {command.Tick}");
    }

    // 시뮬레이션 틱 진행 시 (매 틱마다 호출 — 무거운 로직 금지)
    private void OnSimulationTick(uint tick)
    {
        // 100틱마다 로그 (매 틱 로그는 성능 문제)
        if (tick % 100 == 0)
        {
            Log($"  [Tick] {tick} ({Main.Simulation.GetSimulationTime():F1}초)");
        }
    }

    #endregion

    #region Debug UI

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 350, Screen.height - 20));

        // 상태 표시
        GUILayout.Label($"=== Multiplayer Example ===");
        GUILayout.Label($"Network: {Main.Network.State}");
        GUILayout.Label($"Simulation: {(Main.Simulation.IsRunning ? $"Running (Tick {Main.Simulation.CurrentTick})" : "Stopped")}");
        GUILayout.Label($"Multiplayer: {Main.Command.IsMultiplayer}");
        GUILayout.Label($"Players: {Main.Network.PlayerCount}");
        GUILayout.Label($"Pending Commands: {Main.Command.PendingCommandCount}");

        GUILayout.Space(10);

        // 게임 시작/종료
        GUILayout.Label("--- 게임 시작 ---");

        if (!Main.Simulation.IsRunning)
        {
            if (GUILayout.Button("싱글플레이어 시작"))
                StartSinglePlayer();

            if (GUILayout.Button($"방 생성 (Host): {_roomName}"))
                CreateRoom();

            if (GUILayout.Button($"방 참가 (Client): {_roomName}"))
                JoinRoom();
        }
        else
        {
            if (GUILayout.Button("게임 종료"))
                StopGame();

            if (Main.Simulation.IsPaused)
            {
                if (GUILayout.Button("재개"))
                    Main.Simulation.Resume();
            }
            else
            {
                if (GUILayout.Button("일시정지"))
                    Main.Simulation.Pause();
            }
        }

        GUILayout.Space(10);

        // 명령 전송
        if (Main.Simulation.IsRunning && !Main.Simulation.IsPaused)
        {
            GUILayout.Label("--- 명령 전송 ---");

            if (GUILayout.Button("이동 (10, 5)"))
                SendMoveCommand(new Vector2(10f, 5f));

            if (GUILayout.Button("공격 (target: 99)"))
                SendAttackCommand(99);

            if (GUILayout.Button("건설 (type: 1, pos: 20,20)"))
                SendBuildCommand(1, new Vector2(20f, 20f));

            if (GUILayout.Button("유닛 생산 (type: 5, building: 10)"))
                SendTrainCommand(5, 10);
        }

        GUILayout.Space(10);

        // 로그
        GUILayout.Label("--- Log ---");
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(300));
        GUILayout.Label(_log);
        GUILayout.EndScrollView();

        if (GUILayout.Button("로그 초기화"))
            _log = "";

        GUILayout.EndArea();
    }

    #endregion

    #region Helper

    // 로그 추가
    private void Log(string message)
    {
        _log = $"{message}\n{_log}";

        // 로그가 너무 길어지면 잘라냄
        if (_log.Length > 5000)
            _log = _log[..5000];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[MultiplayerExample] {message}");
#endif
    }

    #endregion
}
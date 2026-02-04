using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if PHOTON_FUSION
using Fusion;
using Fusion.Sockets;
#endif

/// <summary>
/// 네트워크 통신을 담당하는 매니저.
/// Photon Fusion의 NetworkRunner + INetworkRunnerCallbacks 기반으로 세션 관리 및 동기화를 처리합니다.
/// PHOTON_FUSION 미정의 시에도 빈 껍데기로 컴파일되어 Main.Network 접근이 안전합니다.
/// </summary>
public class NetworkManager : CoreManager
#if PHOTON_FUSION
    , INetworkRunnerCallbacks
#endif
{
    #region Fields

    // 현재 네트워크 상태
    private NetworkState _state = NetworkState.Disconnected;

    #endregion

#if PHOTON_FUSION

    #region Fields (Fusion)

    // Fusion NetworkRunner 인스턴스
    private NetworkRunner _runner;

    // 접속 중인 플레이어 데이터
    private readonly Dictionary<PlayerRef, NetworkPlayerData> _players = new();

    // 최대 플레이어 수
    private int _maxPlayers = 4;

    // 로컬 플레이어 이름
    private string _playerName = "Player";

    #endregion

#endif

    #region Properties

    /// <summary>
    /// 현재 네트워크 연결 상태를 반환합니다.
    /// </summary>
    public NetworkState State
    {
        get => _state;
        private set => _state = value;
    }

    // 로비 이상 상태면 연결된 것으로 판단
    public bool IsConnected => State >= NetworkState.InLobby;

#if PHOTON_FUSION
    // 현재 접속 중인 플레이어 수
    public int PlayerCount => _players.Count;
#else
    public int PlayerCount => 0;
#endif

    #endregion

#if PHOTON_FUSION

    #region Properties (Fusion)

    public NetworkRunner Runner => _runner;

    // 현재 클라이언트가 호스트인지 여부
    public bool IsHost => _runner?.IsServer ?? false;

    // 로컬 플레이어 참조
    public PlayerRef LocalPlayer => _runner?.LocalPlayer ?? default;

    // 로컬 플레이어 데이터
    public NetworkPlayerData LocalPlayerData =>
        _runner != null && _players.TryGetValue(_runner.LocalPlayer, out var data) ? data : null;

    #endregion

#endif

    #region Events

    // 네트워크 상태 변경 시 발생
    public event Action<NetworkState> OnStateChanged;

    #endregion

#if PHOTON_FUSION

    #region Events (Fusion)

    // 플레이어 입장 시 발생
    public event Action<PlayerRef, NetworkPlayerData> OnPlayerJoinedEvent;

    // 플레이어 퇴장 시 발생
    public event Action<PlayerRef> OnPlayerLeftEvent;

    // 세션 목록 갱신 시 발생
    public event Action<List<SessionInfo>> OnSessionListUpdated;

    // 네트워크 셧다운 시 발생
    public event Action<ShutdownReason> OnShutdownEvent;

    #endregion

#endif

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        State = NetworkState.Disconnected;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NetworkManager] Initialized");
#endif
    }

    #endregion

#if PHOTON_FUSION

    #region 세션 관리

    /// <summary>
    /// Fusion 로비에 접속합니다.
    /// 접속 후 세션 목록을 탐색할 수 있습니다.
    /// </summary>
    public async UniTask<bool> ConnectToLobbyAsync(string lobbyName = "default")
    {
        if (State != NetworkState.Disconnected)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[NetworkManager] Already connected or connecting");
#endif
            return false;
        }

        SetState(NetworkState.Connecting);

        EnsureRunner();

        var result = await _runner.JoinSessionLobby(SessionLobby.Custom, lobbyName);

        if (result.Ok)
        {
            SetState(NetworkState.InLobby);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Connected to lobby: {lobbyName}");
#endif
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[NetworkManager] Failed to connect to lobby: {result.ShutdownReason}");
#endif

        SetState(NetworkState.Disconnected);
        return false;
    }

    /// <summary>
    /// 새로운 방을 생성합니다. 생성자가 호스트가 됩니다.
    /// </summary>
    /// <param name="roomName">방 이름</param>
    /// <param name="maxPlayers">최대 플레이어 수</param>
    public async UniTask<bool> CreateRoomAsync(string roomName, int maxPlayers = 4)
    {
        _maxPlayers = maxPlayers;

        EnsureRunner();

        SetState(NetworkState.Connecting);

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = roomName,
            PlayerCount = maxPlayers,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            SetState(NetworkState.InRoom);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Room created: {roomName} (max: {maxPlayers})");
#endif
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[NetworkManager] Failed to create room: {result.ShutdownReason}");
#endif

        SetState(NetworkState.Disconnected);
        return false;
    }

    /// <summary>
    /// 이름으로 방에 참가합니다.
    /// </summary>
    /// <param name="roomName">참가할 방 이름</param>
    public async UniTask<bool> JoinRoomAsync(string roomName)
    {
        EnsureRunner();

        SetState(NetworkState.Connecting);

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = roomName,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            SetState(NetworkState.InRoom);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Joined room: {roomName}");
#endif
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[NetworkManager] Failed to join room: {result.ShutdownReason}");
#endif

        SetState(NetworkState.Disconnected);
        return false;
    }

    /// <summary>
    /// SessionInfo를 통해 방에 참가합니다.
    /// </summary>
    /// <param name="session">참가할 세션 정보</param>
    public async UniTask<bool> JoinRoomAsync(SessionInfo session)
    {
        EnsureRunner();

        SetState(NetworkState.Connecting);

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = session.Name,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            SetState(NetworkState.InRoom);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Joined room via SessionInfo: {session.Name}");
#endif
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError($"[NetworkManager] Failed to join room: {result.ShutdownReason}");
#endif

        SetState(NetworkState.Disconnected);
        return false;
    }

    /// <summary>
    /// 현재 방에서 퇴장합니다.
    /// </summary>
    public async UniTask LeaveRoomAsync()
    {
        if (_runner != null)
        {
            await _runner.Shutdown();
        }

        _players.Clear();
        SetState(NetworkState.Disconnected);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NetworkManager] Left room");
#endif
    }

    #endregion

    #region 플레이어 관리

    /// <summary>
    /// 특정 플레이어의 데이터를 반환합니다.
    /// </summary>
    public NetworkPlayerData GetPlayerData(PlayerRef player)
    {
        return _players.TryGetValue(player, out var data) ? data : null;
    }

    /// <summary>
    /// 모든 플레이어 데이터를 반환합니다.
    /// </summary>
    public IReadOnlyDictionary<PlayerRef, NetworkPlayerData> GetAllPlayers()
    {
        return _players;
    }

    // NetworkPlayerData.Spawned()에서 호출
    public void RegisterPlayerData(PlayerRef player, NetworkPlayerData data)
    {
        _players[player] = data;
        OnPlayerJoinedEvent?.Invoke(player, data);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player registered: {player}");
#endif
    }

    // NetworkPlayerData.Despawned()에서 호출
    public void UnregisterPlayerData(PlayerRef player)
    {
        _players.Remove(player);
        OnPlayerLeftEvent?.Invoke(player);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player unregistered: {player}");
#endif
    }

    #endregion

    #region INetworkRunnerCallbacks

    // 플레이어 입장 시 호스트가 NetworkPlayerData 스폰
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var prefab = runner.Config.Simulation.DefaultPlayers;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Spawning PlayerData for: {player}");
#endif

            var obj = runner.Spawn(prefab, inputAuthority: player);
            if (obj.TryGetComponent<NetworkPlayerData>(out var playerData))
            {
                playerData.OwnerRef = player;
            }
        }
    }

    // 플레이어 퇴장 시 호스트가 Despawn 처리
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && _players.TryGetValue(player, out var data))
        {
            runner.Despawn(data.Object);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Player left, despawned: {player}");
#endif
        }
    }

    // Fusion Input 수집 — CommandManager에서 입력 데이터를 가져옴
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        var inputData = Main.Command?.CollectInput() ?? default;
        input.Set(inputData);
    }

    // 세션 목록 갱신
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListUpdated?.Invoke(sessionList);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Session list updated: {sessionList.Count} sessions");
#endif
    }

    // 서버에서 연결 해제됨
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        _players.Clear();
        SetState(NetworkState.Disconnected);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Disconnected from server: {reason}");
#endif
    }

    // 러너 셧다운
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _players.Clear();
        SetState(NetworkState.Disconnected);
        OnShutdownEvent?.Invoke(shutdownReason);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Shutdown: {shutdownReason}");
#endif
    }

    // 나머지 콜백 — 빈 구현
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion

    #region Internal Methods (Fusion)

    // NetworkRunner가 없으면 생성
    private void EnsureRunner()
    {
        if (_runner != null) return;

        var go = new GameObject("@NetworkRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);

        _runner = go.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        if (!go.TryGetComponent<FusionPoolProvider>(out _))
        {
            go.AddComponent<FusionPoolProvider>();
        }
    }

    // NetworkRunner 정리
    private async UniTask CleanupRunner()
    {
        if (_runner != null)
        {
            await _runner.Shutdown();

            if (_runner != null && _runner.gameObject != null)
            {
                UnityEngine.Object.Destroy(_runner.gameObject);
            }

            _runner = null;
        }

        _players.Clear();
    }

    #endregion

#endif

    #region Internal Methods

    // 상태 변경 및 이벤트 발행
    private void SetState(NetworkState newState)
    {
        if (State == newState) return;

        State = newState;
        OnStateChanged?.Invoke(newState);
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        base.Clear();

#if PHOTON_FUSION
        CleanupRunner().Forget();
#endif

        State = NetworkState.Disconnected;
        OnStateChanged = null;

#if PHOTON_FUSION
        OnPlayerJoinedEvent = null;
        OnPlayerLeftEvent = null;
        OnSessionListUpdated = null;
        OnShutdownEvent = null;
#endif
    }

    #endregion
}

#region Network Types

/// <summary>
/// 네트워크 연결 상태
/// </summary>
public enum NetworkState
{
    Disconnected,  // 미연결
    Connecting,    // 연결 중
    InLobby,       // 로비 (방 탐색 중)
    InRoom,        // 방 참가 완료
    InGame         // 게임 진행 중
}

#endregion

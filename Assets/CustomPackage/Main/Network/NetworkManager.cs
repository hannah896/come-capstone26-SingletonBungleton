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

    // 플레이어 데이터 프리팹 Addressable 키
    private const string PLAYER_DATA_PREFAB_KEY = "NetworkPlayerData";

    // 플레이어 캐릭터(조작 대상) 프리팹 Addressable 키
    private const string PLAYER_CHARACTER_PREFAB_KEY = "Player";

    // Fusion NetworkRunner 인스턴스
    private NetworkRunner _runner;

    // 플레이어 입장 시 스폰할 NetworkPlayerData 프리팹 (GameObject로 보관하여 IL Weaver 충돌 방지)
    private GameObject _playerDataPrefab;

    // 플레이어 캐릭터 프리팹 (NetworkObject 포함)
    private GameObject _playerCharacterPrefab;

    // 접속 중인 플레이어 데이터
    private readonly Dictionary<PlayerRef, NetworkPlayerData> _players = new();

    // 이 클라가 스폰한 자신의 플레이어 캐릭터 (Shared 모드: 각자 자기 캐릭터 소유, 중복 스폰 방지)
    private NetworkObject _localCharacter;

    // 월드 생성 완료 여부 (호스트가 캐릭터 스폰 시점을 판단)
    private bool _worldReady;

    // 캐릭터 스폰 위치 (월드 생성 후 호스트가 확정)
    private Vector3 _spawnPoint;

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

    // 방 목록 갱신 시 발생 (Fusion 타입에 의존하지 않는 RoomInfo로 전달)
    public event Action<List<RoomInfo>> OnRoomListUpdated;

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

#if PHOTON_FUSION
        // Addressables에서 NetworkPlayerData 프리팹 로드
        _playerDataPrefab = await Main.Resource.LoadAssetAsync<GameObject>(PLAYER_DATA_PREFAB_KEY, AssetCacheType.Required);

        // 플레이어 캐릭터 프리팹 로드 (NetworkObject 포함되어 있어야 Runner.Spawn 가능)
        _playerCharacterPrefab = await Main.Resource.LoadAssetAsync<GameObject>(PLAYER_CHARACTER_PREFAB_KEY, AssetCacheType.Required);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_playerDataPrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load player data prefab: {PLAYER_DATA_PREFAB_KEY}");
        }
        if (_playerCharacterPrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load player character prefab: {PLAYER_CHARACTER_PREFAB_KEY}");
        }
#endif
#endif

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
    public async UniTask<bool> CreateRoomAsync(RoomCreateArgs args)
    {
        _maxPlayers = args.MaxPlayers;

        EnsureRunner();

        SetState(NetworkState.Connecting);

        // 방 생성자(호스트)가 정한 int 속성(월드 시드/옵션 등)을 세션에 실어 모든 참가자에게 공유
        Dictionary<string, SessionProperty> sessionProps = null;
        if (args.Properties != null && args.Properties.Count > 0)
        {
            sessionProps = new Dictionary<string, SessionProperty>(args.Properties.Count);
            foreach (var kv in args.Properties)
                sessionProps[kv.Key] = kv.Value; // int → SessionProperty 암시적 변환
        }

        var startArgs = new StartGameArgs
        {
            // Shared 모드: 각 클라가 자기 오브젝트의 StateAuthority를 가져 소유자 이동 + NetworkTransform 복제가 성립.
            // (Host 모드에선 호스트만 StateAuthority라 클라의 로컬 이동이 복제되지 않음)
            GameMode = GameMode.Shared,
            SessionName = args.RoomName,
            PlayerCount = args.MaxPlayers,
            SessionProperties = sessionProps,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            SetState(NetworkState.InRoom);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Room created: {args.RoomName} (max: {args.MaxPlayers})");
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
    public async UniTask<bool> JoinRoomAsync(RoomJoinArgs args)
    {
        EnsureRunner();

        SetState(NetworkState.Connecting);

        var startArgs = new StartGameArgs
        {
            // Shared 모드로 같은 세션에 참가 (존재하면 참가, 없으면 생성)
            GameMode = GameMode.Shared,
            SessionName = args.RoomName,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            SetState(NetworkState.InRoom);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Joined room: {args.RoomName}");
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
    public async UniTask<bool> JoinRoomAsync(SessionInfo session)
    {
        return await JoinRoomAsync(new RoomJoinArgs(session.Name));
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

    #region 캐릭터 스폰 (Fusion)

    // Shared 모드: 각 클라가 자기 플레이어를 스폰(스폰한 클라가 State/Input Authority 보유).
    private void SpawnLocalPlayerCharacter()
    {
        if (!_worldReady || _runner == null || _localCharacter != null) return;

        if (_playerCharacterPrefab == null) return;

        var netObj = _playerCharacterPrefab.GetComponent<NetworkObject>();
        if (netObj == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[NetworkManager] Player 프리팹에 NetworkObject가 없습니다. Runner.Spawn 불가.");
#endif
            return;
        }

        _localCharacter = _runner.Spawn(netObj, _spawnPoint, Quaternion.identity);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Local player character spawned at {_spawnPoint}");
#endif
    }

    #endregion

    #region INetworkRunnerCallbacks

    // 플레이어 입장 시 호스트가 NetworkPlayerData 스폰
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            if (_playerDataPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[NetworkManager] PlayerData prefab is not loaded. Cannot spawn for: {player}");
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Spawning PlayerData for: {player}");
#endif

            var networkObj = _playerDataPrefab.GetComponent<NetworkObject>();
            var obj = runner.Spawn(networkObj, inputAuthority: player);
            if (obj.TryGetComponent<NetworkPlayerData>(out var playerData))
            {
                playerData.OwnerRef = player;
            }
        }
        // 캐릭터는 각 클라가 자기 월드 생성 완료 시 NotifyWorldReady에서 스폰한다(Shared 모드).
    }

    // 플레이어 퇴장 처리 (자기 캐릭터는 Fusion이 소유자 이탈 시 정리)
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer && _players.TryGetValue(player, out var data))
        {
            runner.Despawn(data.Object);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player left: {player}");
#endif
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

        // Fusion 타입에 의존하지 않는 RoomInfo로 변환해 UI에 전달
        var rooms = new List<RoomInfo>(sessionList.Count);
        foreach (var session in sessionList)
        {
            // 목록에 노출되고 참가 가능한 방만 (닫힌/숨김 세션 제외)
            if (!session.IsVisible) continue;
            rooms.Add(new RoomInfo(session.Name, session.PlayerCount, session.MaxPlayers));
        }
        OnRoomListUpdated?.Invoke(rooms);

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
        _localCharacter = null;
        _worldReady = false;
    }

    #endregion

#endif

    #region 방 탐색 (UI 진입점)

    /// <summary>
    /// 방 목록 탐색을 시작합니다. 아직 로비에 없으면 로비에 접속합니다.
    /// 접속 후 방 목록은 OnRoomListUpdated 이벤트로 전달됩니다.
    /// </summary>
    public async UniTask<bool> BrowseRoomsAsync(string lobbyName = "default")
    {
#if PHOTON_FUSION
        if (State >= NetworkState.InLobby) return true;
        return await ConnectToLobbyAsync(lobbyName);
#else
        await UniTask.CompletedTask;
        return false;
#endif
    }

    /// <summary>
    /// 방 이름으로 참가합니다. (UI에서 방 클릭 시 호출)
    /// </summary>
    public async UniTask<bool> JoinRoomByNameAsync(string roomName)
    {
#if PHOTON_FUSION
        return await JoinRoomAsync(new RoomJoinArgs(roomName));
#else
        await UniTask.CompletedTask;
        return false;
#endif
    }

    /// <summary>
    /// 호스트로 방을 생성합니다. (UI에서 방 만들기 시 호출)
    /// properties: 세션에 공유할 int 속성(월드 시드/옵션 등).
    /// </summary>
    public async UniTask<bool> HostRoomAsync(
        string roomName, int maxPlayers, IReadOnlyDictionary<string, int> properties = null)
    {
#if PHOTON_FUSION
        return await CreateRoomAsync(new RoomCreateArgs(roomName, maxPlayers, properties));
#else
        await UniTask.CompletedTask;
        return false;
#endif
    }

    #endregion

    #region 월드/캐릭터 스폰 (GameScene 진입점)

    // 현재 방(세션)에 참가한 멀티플레이 상태인지 여부
    public bool IsInRoom => State >= NetworkState.InRoom;

    /// <summary>
    /// 세션에 공유된 int 속성을 읽습니다. (월드 시드/옵션 등)
    /// </summary>
    public bool TryGetSessionInt(string key, out int value)
    {
        value = 0;
#if PHOTON_FUSION
        SessionInfo info = _runner != null ? _runner.SessionInfo : null;
        if (info != null && info.IsValid && info.Properties != null &&
            info.Properties.TryGetValue(key, out var prop))
        {
            value = prop; // SessionProperty → int 암시적 변환
            return true;
        }
#endif
        return false;
    }

    /// <summary>
    /// GameScene에서 월드 생성이 끝난 뒤 호출합니다.
    /// 호스트면 스폰 위치를 확정하고 접속 중인 모든 플레이어의 캐릭터를 스폰합니다.
    /// (클라이언트는 캐릭터가 네트워크로 복제되어 도착하므로 별도 처리 없음)
    /// </summary>
    public void NotifyWorldReady(Vector3 spawnPoint)
    {
#if PHOTON_FUSION
        _spawnPoint = spawnPoint;
        _worldReady = true;

        // Shared 모드: 각 클라가 자기 캐릭터를 스폰한다(호스트가 전원 스폰하지 않음).
        SpawnLocalPlayerCharacter();
#endif
    }

    #endregion

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
        _playerDataPrefab = null;
        _playerCharacterPrefab = null;
        _localCharacter = null;
        _worldReady = false;
        Main.Resource?.Release(PLAYER_DATA_PREFAB_KEY);
        Main.Resource?.Release(PLAYER_CHARACTER_PREFAB_KEY);
#endif

        State = NetworkState.Disconnected;
        OnStateChanged = null;
        OnRoomListUpdated = null;

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

/// <summary>
/// 방 생성 파라미터 (값형식)
/// </summary>
public readonly struct RoomCreateArgs
{
    public readonly string RoomName;
    public readonly int MaxPlayers;

    // 세션에 공유할 int 속성(월드 시드/브랜치/루프/사이즈 등). NetworkManager는 값을 해석하지 않고 전달만 한다.
    public readonly System.Collections.Generic.IReadOnlyDictionary<string, int> Properties;

    public RoomCreateArgs(
        string roomName,
        int maxPlayers = 4,
        System.Collections.Generic.IReadOnlyDictionary<string, int> properties = null)
    {
        RoomName = roomName;
        MaxPlayers = maxPlayers;
        Properties = properties;
    }
}

/// <summary>
/// 방 참가 파라미터 (값형식)
/// </summary>
public readonly struct RoomJoinArgs
{
    public readonly string RoomName;

    public RoomJoinArgs(string roomName)
    {
        RoomName = roomName;
    }
}

/// <summary>
/// 방 목록 표시용 정보 (Fusion SessionInfo를 UI에 노출하지 않기 위한 값형식)
/// </summary>
public readonly struct RoomInfo
{
    public readonly string Name;
    public readonly int PlayerCount;
    public readonly int MaxPlayers;

    // 정원 미달이면 참가 가능
    public bool IsJoinable => PlayerCount < MaxPlayers;

    public RoomInfo(string name, int playerCount, int maxPlayers)
    {
        Name = name;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
    }
}

#endregion

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
///
/// [Host 모드]
/// - 방 생성자는 GameMode.Host, 참가자는 GameMode.Client로 세션을 시작한다.
/// - 모든 NetworkObject의 스폰/디스폰과 상태(StateAuthority)는 호스트가 전담한다.
/// - 클라이언트는 자기 오브젝트에 InputAuthority만 가지며, 상태 변경은 RPC 또는 Fusion Input으로 호스트에 요청한다.
///
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

    // 로컬에서 선택한 캐릭터 성별 (PlayerCharacter 값, 기본 여자) — 대기방 UI가 갱신
    private int _localCharacterIndex = (int)PlayerCharacter.Female;

    // 로컬 플레이어 이름
    private string _playerName = "Player";

    // 방 탐색/생성이 공유하는 커스텀 로비 이름.
    // 호스트가 같은 로비에 세션을 만들어야 탐색 목록에 보인다.
    private const string DEFAULT_LOBBY = "default";

    #endregion

#if PHOTON_FUSION

    #region Fields (Fusion)

    // 플레이어 데이터 프리팹 Addressable 키
    private const string PLAYER_DATA_PREFAB_KEY = "NetworkPlayerData";

    // 플레이어 캐릭터(조작 대상) 프리팹 Addressable 키 (여자, 기본)
    private const string PLAYER_CHARACTER_PREFAB_KEY = "Player";

    // 남자 캐릭터 프리팹 Addressable 키
    private const string PLAYER_CHARACTER_MALE_PREFAB_KEY = "Player_Male";

    // 몬스터 복제 디렉터 프리팹 Addressable 키 (세션당 1개, 호스트가 스폰)
    private const string MONSTER_DIRECTOR_PREFAB_KEY = "NetworkMonsterDirector";

    // Fusion NetworkRunner 인스턴스
    private NetworkRunner _runner;

    // 플레이어 입장 시 스폰할 NetworkPlayerData 프리팹 (GameObject로 보관하여 IL Weaver 충돌 방지)
    private GameObject _playerDataPrefab;

    // 플레이어 캐릭터 프리팹 (NetworkObject 포함, 여자 기본)
    private GameObject _playerCharacterPrefab;

    // 남자 캐릭터 프리팹 (NetworkObject 포함)
    private GameObject _playerCharacterMalePrefab;

    // 몬스터 복제 디렉터 프리팹 (NetworkObject 포함)
    private GameObject _monsterDirectorPrefab;

    // 호스트가 스폰한 몬스터 디렉터 (세션당 1개)
    private NetworkObject _monsterDirector;

    // 게임 시작 처리 중 여부 (Rpc_StartGame 중복 수신 가드)
    private bool _gameStarting;

    // 접속 중인 플레이어 데이터
    private readonly Dictionary<PlayerRef, NetworkPlayerData> _players = new();

    // 호스트가 스폰한 플레이어 캐릭터들 (퇴장 시 호스트가 디스폰)
    private readonly Dictionary<PlayerRef, NetworkObject> _characters = new();

    // 이 클라의 조작 대상 캐릭터 (OnInput에서 transform을 읽어 호스트로 보고)
    private NetworkObject _localCharacter;

    // 월드 생성 완료 여부 (호스트가 캐릭터 스폰 시점을 판단)
    private bool _worldReady;

    // 캐릭터 스폰 위치 (월드 생성 후 호스트가 확정)
    private Vector3 _spawnPoint;

    // 최대 플레이어 수
    private int _maxPlayers = 4;

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

    // 이 피어가 호스트(서버)인지 여부
    public bool IsHost => _runner != null && _runner.IsServer;
#else
    public int PlayerCount => 0;
    public bool IsHost => false;
#endif

    // 현재 방(세션)에 참가한 멀티플레이 상태인지 여부
    public bool IsInRoom => State >= NetworkState.InRoom;

    // 로컬에서 선택한 캐릭터 성별 (PlayerCharacter 값)
    public int LocalCharacterIndex => _localCharacterIndex;

    // 로컬 플레이어 이름 (NetworkPlayerData가 호스트로 전달할 때 참조)
    public string LocalPlayerName => _playerName;

    #endregion

#if PHOTON_FUSION

    #region Properties (Fusion)

    public NetworkRunner Runner => _runner;

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

    // 대기방 플레이어 구성/표시 값 변경 시 발생 (입장/퇴장/이름/성별/방장 변경 — UI는 GetWaitingPlayers()로 리빌드)
    public event Action OnWaitingPlayersChanged;

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

        // 남자 캐릭터 프리팹 로드 (대기방에서 남자 선택 시 스폰)
        _playerCharacterMalePrefab = await Main.Resource.LoadAssetAsync<GameObject>(PLAYER_CHARACTER_MALE_PREFAB_KEY, AssetCacheType.Required);

        // 몬스터 복제 디렉터 프리팹 로드 (호스트가 월드 준비 후 1개 스폰)
        _monsterDirectorPrefab = await Main.Resource.LoadAssetAsync<GameObject>(MONSTER_DIRECTOR_PREFAB_KEY, AssetCacheType.Required);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_playerDataPrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load player data prefab: {PLAYER_DATA_PREFAB_KEY}");
        }
        if (_playerCharacterPrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load player character prefab: {PLAYER_CHARACTER_PREFAB_KEY}");
        }
        if (_playerCharacterMalePrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load player character prefab: {PLAYER_CHARACTER_MALE_PREFAB_KEY}");
        }
        if (_monsterDirectorPrefab == null)
        {
            Debug.LogError($"[NetworkManager] Failed to load monster director prefab: {MONSTER_DIRECTOR_PREFAB_KEY}");
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
    public async UniTask<bool> ConnectToLobbyAsync(string lobbyName = DEFAULT_LOBBY)
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
    /// 새로운 방을 생성합니다. 생성자가 호스트(GameMode.Host)가 됩니다.
    /// </summary>
    public async UniTask<bool> CreateRoomAsync(RoomCreateArgs args)
    {
        _maxPlayers = args.MaxPlayers;

        // 로비 탐색 등에 이미 쓴 러너는 StartGame에 재사용할 수 없다 → 항상 새 러너로 시작한다.
        await CleanupRunner();
        EnsureRunner();

        SetState(NetworkState.Connecting);

        // 호스트가 정한 int 속성(월드 시드/옵션 등)을 세션에 실어 모든 참가자에게 공유
        Dictionary<string, SessionProperty> sessionProps = null;
        if (args.Properties != null && args.Properties.Count > 0)
        {
            sessionProps = new Dictionary<string, SessionProperty>(args.Properties.Count);
            foreach (var kv in args.Properties)
                sessionProps[kv.Key] = kv.Value; // int → SessionProperty 암시적 변환
        }

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = args.RoomName,
            PlayerCount = args.MaxPlayers,
            SessionProperties = sessionProps,
            CustomLobbyName = DEFAULT_LOBBY,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            _gameStarting = false;
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
    /// 이름으로 방에 참가합니다. 참가자는 클라이언트(GameMode.Client)로 접속합니다.
    /// </summary>
    public async UniTask<bool> JoinRoomAsync(RoomJoinArgs args)
    {
        // 로비 탐색에 쓴 러너를 그대로 StartGame에 넘기면 "NetworkRunner should not be reused"가 발생한다.
        await CleanupRunner();
        EnsureRunner();

        SetState(NetworkState.Connecting);

        var startArgs = new StartGameArgs
        {
            // Host 모드 세션의 참가자는 Client로 접속한다 (Host로 시작하면 참가가 아니라 새 세션 생성 시도가 된다)
            GameMode = GameMode.Client,
            SessionName = args.RoomName,
            CustomLobbyName = DEFAULT_LOBBY,
            ObjectProvider = _runner.GetComponent<FusionPoolProvider>()
        };

        var result = await _runner.StartGame(startArgs);

        if (result.Ok)
        {
            _gameStarting = false;
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
    /// 셧다운된 러너는 재사용할 수 없으므로 파괴까지 수행한다 (다음 방 생성/참가 시 새로 생성).
    /// 호스트가 퇴장하면 세션이 종료되어 모든 클라이언트가 OnShutdown을 수신한다.
    /// </summary>
    public async UniTask LeaveRoomAsync()
    {
        await CleanupRunner();

        _gameStarting = false;
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

    // NetworkPlayerData.Spawned()에서 호출 (호스트/클라 공통 — 복제 도착 시점에 등록)
    public void RegisterPlayerData(PlayerRef player, NetworkPlayerData data)
    {
        _players[player] = data;
        OnPlayerJoinedEvent?.Invoke(player, data);
        OnWaitingPlayersChanged?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player registered: {player}");
#endif
    }

    // NetworkPlayerData.Despawned()에서 호출
    public void UnregisterPlayerData(PlayerRef player)
    {
        _players.Remove(player);
        OnPlayerLeftEvent?.Invoke(player);
        OnWaitingPlayersChanged?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player unregistered: {player}");
#endif
    }

    // NetworkPlayerData.Render()의 변경 감지에서 호출 — 대기방 표시 값(이름/성별/방장) 변경 알림
    public void NotifyPlayerDataChanged(NetworkPlayerData data)
    {
        OnWaitingPlayersChanged?.Invoke();
    }

    // NetworkPlayerSync.Spawned()에서 호출 — 이 클라가 조작하는 캐릭터 등록 (OnInput 수집 대상)
    public void RegisterLocalCharacter(NetworkObject character)
    {
        _localCharacter = character;
    }

    // NetworkPlayerSync.Despawned()에서 호출
    public void UnregisterLocalCharacter(NetworkObject character)
    {
        if (_localCharacter == character)
            _localCharacter = null;
    }

    #endregion

    #region 캐릭터 스폰 (호스트 전담)

    // 호스트가 특정 플레이어의 캐릭터를 스폰한다. (InputAuthority = 해당 플레이어)
    private void SpawnCharacterForPlayer(PlayerRef player)
    {
        if (!_worldReady || _runner == null || !_runner.IsServer) return;
        if (_characters.ContainsKey(player)) return;

        // 대기방에서 각 플레이어가 고른 성별로 프리팹 분기 (남자 프리팹 로드 실패 시 여자로 폴백)
        int characterIndex = GetPlayerData(player)?.CharacterIndex ?? (int)PlayerCharacter.Female;
        GameObject prefab =
            characterIndex == (int)PlayerCharacter.Male && _playerCharacterMalePrefab != null
                ? _playerCharacterMalePrefab
                : _playerCharacterPrefab;

        if (prefab == null) return;

        if (!prefab.TryGetComponent<NetworkObject>(out var netObj))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[NetworkManager] Player 프리팹에 NetworkObject가 없습니다. Runner.Spawn 불가.");
#endif
            return;
        }

        var character = _runner.Spawn(netObj, _spawnPoint, Quaternion.identity, inputAuthority: player);
        _characters[player] = character;

        // 호스트 자신의 캐릭터는 입력 수집 대상으로도 등록
        if (player == _runner.LocalPlayer)
            _localCharacter = character;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Character spawned for {player} at {_spawnPoint}");
#endif
    }

    // 호스트가 몬스터 복제 디렉터를 세션당 1개 스폰한다.
    // 몬스터 프리팹에는 NetworkObject를 붙이지 않고, 이 디렉터 하나가 전체 몬스터 상태를 복제한다.
    private void SpawnMonsterDirector()
    {
        if (_runner == null || !_runner.IsServer) return;
        if (_monsterDirector != null) return;
        if (_monsterDirectorPrefab == null) return;

        if (!_monsterDirectorPrefab.TryGetComponent<NetworkObject>(out var netObj))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[NetworkManager] MonsterDirector 프리팹에 NetworkObject가 없습니다. Runner.Spawn 불가.");
#endif
            return;
        }

        _monsterDirector = _runner.Spawn(netObj);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NetworkManager] MonsterDirector spawned");
#endif
    }

    // 호스트가 접속 중인 모든 플레이어의 캐릭터를 스폰한다.
    private void SpawnAllCharacters()
    {
        if (_runner == null || !_runner.IsServer) return;

        foreach (var player in _runner.ActivePlayers)
            SpawnCharacterForPlayer(player);
    }

    #endregion

    #region INetworkRunnerCallbacks

    // 플레이어 입장 — 호스트만 NetworkPlayerData를 스폰한다 (Host 모드: 클라는 Runner.Spawn 불가)
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

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

        // Spawn 도중 Spawned() → RegisterPlayerData가 실행되므로 onBeforeSpawned에서 선기록.
        // 이름/성별은 호스트 자신 것만 즉시 기록하고, 클라 것은 클라가 Spawned() 후 RPC로 올려준다.
        runner.Spawn(networkObj, inputAuthority: player, onBeforeSpawned: (r, obj) =>
        {
            if (!obj.TryGetComponent<NetworkPlayerData>(out var playerData)) return;

            playerData.OwnerRef = player;
            playerData.IsMaster = player == r.LocalPlayer; // Host 모드: 호스트가 곧 방장

            if (player == r.LocalPlayer)
            {
                playerData.WriteNameInternal(_playerName);
                playerData.CharacterIndex = _localCharacterIndex;
            }
        });

        // 게임 중 재입장 대비: 월드가 이미 준비됐다면 캐릭터도 즉시 스폰
        // (대기방 단계에서는 _worldReady == false라 무시된다)
        SpawnCharacterForPlayer(player);
    }

    // 플레이어 퇴장 — 호스트가 해당 플레이어의 데이터/캐릭터를 디스폰한다.
    // (Host 모드: StateAuthority는 항상 호스트라 자동 정리 플래그가 동작하지 않으므로 명시적으로 정리)
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            if (_players.TryGetValue(player, out var data) && data != null && data.Object != null)
                runner.Despawn(data.Object);

            if (_characters.TryGetValue(player, out var character))
            {
                _characters.Remove(player);
                if (character != null)
                    runner.Despawn(character);
            }
        }

        OnWaitingPlayersChanged?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Player left: {player}");
#endif
    }

    // Fusion Input 수집 — 명령(CommandManager) + 로컬 캐릭터 위치 보고
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
    {
        var inputData = Main.Command?.CollectInput() ?? default;

        // 로컬 시뮬레이션 결과(위치/Yaw)를 호스트로 보고 → 호스트가 확정 후 전 클라에 복제
        if (_localCharacter != null)
        {
            Transform t = _localCharacter.transform;
            inputData.CharacterPosition = t.position;
            inputData.CharacterYaw = t.eulerAngles.y;
            inputData.HasCharacterState = true;
        }

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

    // 서버에서 연결 해제됨 (클라이언트: 호스트 종료/강퇴 등)
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        _players.Clear();
        _characters.Clear();
        _localCharacter = null;
        SetState(NetworkState.Disconnected);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Disconnected from server: {reason}");
#endif
    }

    // 러너 셧다운 — 셧다운된 러너는 재사용할 수 없으므로 파괴하고 참조를 비운다 (다음 접속 시 새로 생성)
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        _players.Clear();
        _characters.Clear();
        _localCharacter = null;
        _monsterDirector = null;
        _worldReady = false;
        _gameStarting = false;

        if (_runner == runner)
        {
            _runner = null;
            if (runner != null && runner.gameObject != null)
                UnityEngine.Object.Destroy(runner.gameObject);
        }

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

        // NetworkManager는 MonoBehaviour가 아니어서 러너 오브젝트에서 자동 탐색되지 않는다.
        // 명시적으로 등록해야 OnSessionListUpdated/OnPlayerJoined/OnShutdown 등이 호출된다.
        _runner.AddCallbacks(this);

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
            var runner = _runner;
            await runner.Shutdown(); // OnShutdown 콜백에서 _runner 해제/파괴가 수행될 수 있다

            if (_runner == runner)
            {
                _runner = null;
                if (runner != null && runner.gameObject != null)
                    UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        _players.Clear();
        _characters.Clear();
        _localCharacter = null;
        _monsterDirector = null;
        _worldReady = false;
    }

    #endregion

#endif

    #region 방 탐색 (UI 진입점)

    /// <summary>
    /// 방 목록 탐색을 시작합니다. 아직 로비에 없으면 로비에 접속합니다.
    /// 접속 후 방 목록은 OnRoomListUpdated 이벤트로 전달됩니다.
    /// </summary>
    public async UniTask<bool> BrowseRoomsAsync(string lobbyName = DEFAULT_LOBBY)
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

    #region 대기방 (UI 진입점)

    /// <summary>
    /// 현재 참가 중인 방 이름을 반환합니다.
    /// </summary>
    public string CurrentRoomName
    {
        get
        {
#if PHOTON_FUSION
            SessionInfo info = _runner != null ? _runner.SessionInfo : null;
            return info != null && info.IsValid ? info.Name : string.Empty;
#else
            return string.Empty;
#endif
        }
    }

    /// <summary>
    /// 로컬 플레이어 이름을 설정합니다. (방 입장 전 UI에서 호출)
    /// 이미 방에 있다면 네트워크 데이터에도 반영됩니다.
    /// </summary>
    public void SetLocalPlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return;

        _playerName = playerName;

#if PHOTON_FUSION
        LocalPlayerData?.SetName(playerName);
#endif
    }

    /// <summary>
    /// 로컬 플레이어의 캐릭터 성별을 선택합니다. (대기방 UI에서 호출)
    /// 선택 값은 네트워크 데이터에도 반영되어 다른 플레이어의 대기방 목록에 표시됩니다.
    /// </summary>
    public void SetLocalCharacter(int characterIndex)
    {
        _localCharacterIndex = characterIndex;

#if PHOTON_FUSION
        LocalPlayerData?.SetCharacter(characterIndex);
#endif
    }

    /// <summary>
    /// 대기방 표시용 플레이어 목록을 반환합니다. (PlayerId 오름차순, Fusion 타입 미노출)
    /// </summary>
    public List<WaitingPlayerInfo> GetWaitingPlayers()
    {
        var list = new List<WaitingPlayerInfo>();

#if PHOTON_FUSION
        foreach (var pair in _players)
        {
            NetworkPlayerData data = pair.Value;
            if (data == null) continue;

            list.Add(new WaitingPlayerInfo(
                pair.Key.PlayerId, data.PlayerName, data.CharacterIndex, data.IsMaster, data.IsLocal));
        }

        list.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
#endif

        return list;
    }

    /// <summary>
    /// 게임 시작을 요청합니다. (대기방 시작 버튼 — 호스트만 가능)
    /// 방을 닫아 늦은 참가를 차단하고, RPC로 전 클라이언트에 시작을 알립니다.
    /// </summary>
    public void RequestStartGame()
    {
#if PHOTON_FUSION
        if (!IsHost || _gameStarting) return;

        SessionInfo session = _runner != null ? _runner.SessionInfo : null;
        if (session != null && session.IsValid)
        {
            session.IsOpen = false; // 시작 이후 늦은 참가 차단
        }

        LocalPlayerData?.Rpc_StartGame();
#endif
    }

#if PHOTON_FUSION
    /// <summary>
    /// Rpc_StartGame 수신 처리 (호스트 포함 전 클라이언트).
    /// 세션에 공유된 월드 시드/옵션을 반영하고 게임씬으로 전환합니다.
    /// </summary>
    public void HandleGameStartReceived()
    {
        if (_gameStarting || State != NetworkState.InRoom) return;

        _gameStarting = true;
        SetState(NetworkState.InGame);

        // 호스트/클라 단일 경로: 자기 SessionInfo에서 시드/옵션을 읽어 WorldGenRequest에 반영
        NetworkWorldConfig.ApplyFromSession();
        Extensions.ChangeScene("GameScene");
    }
#endif

    #endregion

    #region 월드/캐릭터 스폰 (GameScene 진입점)

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

        // 몬스터 복제 디렉터를 먼저 띄운다 (스포너가 등록할 대상이 있어야 한다)
        SpawnMonsterDirector();

        // Host 모드: 호스트가 전원 캐릭터를 스폰한다 (InputAuthority만 각 플레이어에게 부여)
        SpawnAllCharacters();
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
        _playerCharacterMalePrefab = null;
        Main.Resource?.Release(PLAYER_DATA_PREFAB_KEY);
        Main.Resource?.Release(PLAYER_CHARACTER_PREFAB_KEY);
        Main.Resource?.Release(PLAYER_CHARACTER_MALE_PREFAB_KEY);
#endif

        State = NetworkState.Disconnected;
        OnStateChanged = null;
        OnRoomListUpdated = null;
        OnWaitingPlayersChanged = null;

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

/// <summary>
/// 대기방 표시용 플레이어 정보 (Fusion 타입을 UI에 노출하지 않기 위한 값형식)
/// </summary>
public readonly struct WaitingPlayerInfo
{
    public readonly int PlayerId;
    public readonly string PlayerName;
    public readonly int CharacterIndex; // PlayerCharacter 값
    public readonly bool IsMaster;      // 방장(호스트) 여부
    public readonly bool IsLocal;       // 이 클라이언트 자신 여부

    public WaitingPlayerInfo(int playerId, string playerName, int characterIndex, bool isMaster, bool isLocal)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        CharacterIndex = characterIndex;
        IsMaster = isMaster;
        IsLocal = isLocal;
    }
}

#endregion

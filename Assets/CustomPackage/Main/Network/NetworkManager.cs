using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 네트워크 통신을 담당하는 매니저.
/// 서버와의 명령 송수신 및 게임 상태 동기화를 처리합니다.
/// </summary>
public class NetworkManager : CoreManager
{
    #region Fields

    // 전송 대기 중인 명령 큐
    private readonly Queue<GameCommand> _outgoingCommands = new();

    // 네트워크 어댑터 (실제 통신 구현)
    private INetworkAdapter _adapter;

    #endregion

    #region Properties

    // 현재 연결 상태
    public NetworkState State { get; private set; } = NetworkState.Disconnected;

    // 현재 핑 (밀리초)
    public int Ping { get; private set; }

    // 서버 연결 여부를 State로부터 계산
    public bool IsConnected => State == NetworkState.Connected;

    #endregion

    #region Events

    // 연결 상태 변경 시 발생
    public event Action<NetworkState> OnStateChanged;

    // 서버로부터 명령 수신 시 발생
    public event Action<GameCommand> OnCommandReceived;

    // 게임 상태 스냅샷 수신 시 발생
    public event Action<GameStateSnapshot> OnStateSnapshotReceived;

    // 연결 해제 시 발생
    public event Action<DisconnectReason> OnDisconnected;

    #endregion

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

    #region Configuration

    /// <summary>
    /// 네트워크 어댑터를 설정합니다.
    /// </summary>
    public void SetAdapter(INetworkAdapter adapter)
    {
        if (_adapter != null)
        {
            _adapter.OnConnected -= HandleConnected;
            _adapter.OnDisconnected -= HandleDisconnected;
            _adapter.OnDataReceived -= HandleDataReceived;
        }

        _adapter = adapter;

        if (_adapter != null)
        {
            _adapter.OnConnected += HandleConnected;
            _adapter.OnDisconnected += HandleDisconnected;
            _adapter.OnDataReceived += HandleDataReceived;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Adapter set: {adapter?.GetType().Name ?? "null"}");
#endif
    }

    #endregion

    #region Connection

    /// <summary>
    /// 서버에 연결합니다.
    /// </summary>
    public async UniTask<bool> ConnectAsync(string host, int port)
    {
        if (_adapter == null)
        {
            Debug.LogError("[NetworkManager] No adapter set");
            return false;
        }

        if (State != NetworkState.Disconnected)
        {
            Debug.LogWarning("[NetworkManager] Already connecting or connected");
            return false;
        }

        SetState(NetworkState.Connecting);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Connecting to {host}:{port}...");
#endif

        bool success = await _adapter.ConnectAsync(host, port);

        if (!success)
        {
            SetState(NetworkState.Disconnected);
        }

        return success;
    }

    /// <summary>
    /// 서버와의 연결을 종료합니다.
    /// </summary>
    public void Disconnect()
    {
        if (State == NetworkState.Disconnected) return;

        _adapter?.Disconnect();
        SetState(NetworkState.Disconnected);
        _outgoingCommands.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NetworkManager] Disconnected");
#endif
    }

    #endregion

    #region Send

    /// <summary>
    /// 명령을 서버로 전송합니다.
    /// </summary>
    public void SendCommand(GameCommand command, bool reliable = true)
    {
        if (!IsConnected)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[NetworkManager] Cannot send command: not connected");
#endif
            return;
        }

        var packet = NetworkPacket.CreateCommandPacket(command);
        _adapter?.Send(packet.Serialize(), reliable);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Command sent: {command.Type} (reliable: {reliable})");
#endif
    }

    /// <summary>
    /// 여러 명령을 배치로 전송합니다.
    /// </summary>
    public void SendCommands(IEnumerable<GameCommand> commands, bool reliable = true)
    {
        if (!IsConnected) return;

        var packet = NetworkPacket.CreateBatchCommandPacket(commands);
        _adapter?.Send(packet.Serialize(), reliable);
    }

    /// <summary>
    /// 게임 참가 요청을 보냅니다.
    /// </summary>
    public void RequestJoinGame(string roomId, string playerName)
    {
        if (!IsConnected) return;

        var packet = NetworkPacket.CreateJoinRequest(roomId, playerName);
        _adapter?.Send(packet.Serialize(), reliable: true);
    }

    #endregion

    #region Event Handlers

    // 연결 성공 처리
    private void HandleConnected()
    {
        SetState(NetworkState.Connected);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[NetworkManager] Connected to server");
#endif
    }

    // 연결 해제 처리
    private void HandleDisconnected(DisconnectReason reason)
    {
        SetState(NetworkState.Disconnected);
        OnDisconnected?.Invoke(reason);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkManager] Disconnected: {reason}");
#endif
    }

    // 데이터 수신 처리
    private void HandleDataReceived(byte[] data)
    {
        var packet = NetworkPacket.Deserialize(data);
        if (packet == null) return;

        switch (packet.Type)
        {
            case PacketType.Command:
                ProcessCommandPacket(packet);
                break;

            case PacketType.BatchCommand:
                ProcessBatchCommandPacket(packet);
                break;

            case PacketType.StateSnapshot:
                ProcessStateSnapshotPacket(packet);
                break;

            case PacketType.Ping:
                Ping = packet.GetPing();
                break;

            case PacketType.JoinResponse:
                ProcessJoinResponse(packet);
                break;
        }
    }

    // 단일 명령 패킷 처리
    private void ProcessCommandPacket(NetworkPacket packet)
    {
        var command = packet.GetCommand();
        OnCommandReceived?.Invoke(command);
        Main.Command?.OnCommandReceivedFromServer(command);
    }

    // 배치 명령 패킷 처리
    private void ProcessBatchCommandPacket(NetworkPacket packet)
    {
        foreach (var cmd in packet.GetCommands())
        {
            OnCommandReceived?.Invoke(cmd);
            Main.Command?.OnCommandReceivedFromServer(cmd);
        }
    }

    // 상태 스냅샷 패킷 처리
    private void ProcessStateSnapshotPacket(NetworkPacket packet)
    {
        var snapshot = packet.GetStateSnapshot();
        OnStateSnapshotReceived?.Invoke(snapshot);
    }

    // 게임 참가 응답 처리
    private void ProcessJoinResponse(NetworkPacket packet)
    {
        var (success, playerId, errorMessage) = packet.GetJoinResponse();

        if (success)
        {
            Main.Command?.SetMultiplayerMode(true, playerId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NetworkManager] Joined game as Player {playerId}");
#endif
        }
        else
        {
            Debug.LogError($"[NetworkManager] Failed to join game: {errorMessage}");
        }
    }

    #endregion

    #region Internal Methods

    // 상태 변경
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

        Disconnect();

        if (_adapter != null)
        {
            _adapter.OnConnected -= HandleConnected;
            _adapter.OnDisconnected -= HandleDisconnected;
            _adapter.OnDataReceived -= HandleDataReceived;
            _adapter = null;
        }

        OnStateChanged = null;
        OnCommandReceived = null;
        OnStateSnapshotReceived = null;
        OnDisconnected = null;
    }

    #endregion
}

#region Network Types

/// <summary>
/// 네트워크 연결 상태
/// </summary>
public enum NetworkState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// 연결 해제 사유
/// </summary>
public enum DisconnectReason
{
    None,
    ClientRequest,
    ServerClosed,
    Timeout,
    Error
}

/// <summary>
/// 패킷 타입
/// </summary>
public enum PacketType : byte
{
    Command,
    BatchCommand,
    StateSnapshot,
    Ping,
    JoinRequest,
    JoinResponse,
    LeaveRequest,
    LeaveResponse
}

#endregion

#region Interfaces

/// <summary>
/// 네트워크 어댑터 인터페이스.
/// 실제 네트워크 라이브러리(Photon, Mirror, Netcode 등)를 래핑합니다.
/// </summary>
public interface INetworkAdapter
{
    event Action OnConnected;
    event Action<DisconnectReason> OnDisconnected;
    event Action<byte[]> OnDataReceived;

    UniTask<bool> ConnectAsync(string host, int port);
    void Disconnect();
    void Send(byte[] data, bool reliable);
}

#endregion

#region Data Structures

/// <summary>
/// 게임 상태 스냅샷.
/// 서버에서 클라이언트로 전체 게임 상태를 동기화할 때 사용합니다.
/// </summary>
[Serializable]
public struct GameStateSnapshot
{
    public uint Tick;
    public byte[] Data;
}

/// <summary>
/// 네트워크 패킷.
/// 데이터 직렬화/역직렬화를 담당합니다.
/// </summary>
public class NetworkPacket
{
    #region Fields

    // 패킷 데이터
    private byte[] _payload;

    #endregion

    #region Properties

    public PacketType Type { get; private set; }

    #endregion

    #region Factory Methods

    // 명령 패킷 생성
    public static NetworkPacket CreateCommandPacket(GameCommand command)
    {
        return new NetworkPacket
        {
            Type = PacketType.Command,
            _payload = SerializeCommand(command)
        };
    }

    // 배치 명령 패킷 생성
    public static NetworkPacket CreateBatchCommandPacket(IEnumerable<GameCommand> commands)
    {
        // TODO: 실제 직렬화 구현
        return new NetworkPacket
        {
            Type = PacketType.BatchCommand,
            _payload = new byte[0]
        };
    }

    // 게임 참가 요청 패킷 생성
    public static NetworkPacket CreateJoinRequest(string roomId, string playerName)
    {
        // TODO: 실제 직렬화 구현
        return new NetworkPacket
        {
            Type = PacketType.JoinRequest,
            _payload = new byte[0]
        };
    }

    #endregion

    #region Serialization

    // 패킷을 바이트 배열로 직렬화
    public byte[] Serialize()
    {
        // TODO: 실제 직렬화 구현
        return new byte[0];
    }

    // 바이트 배열에서 패킷 역직렬화
    public static NetworkPacket Deserialize(byte[] data)
    {
        // TODO: 실제 역직렬화 구현
        return null;
    }

    // 명령 직렬화
    private static byte[] SerializeCommand(GameCommand command)
    {
        // TODO: 실제 직렬화 구현 (BinaryWriter, MessagePack, Protobuf 등)
        return new byte[0];
    }

    // 명령 역직렬화
    private static GameCommand DeserializeCommand(byte[] data)
    {
        // TODO: 실제 역직렬화 구현
        return default;
    }

    #endregion

    #region Data Extraction

    // 단일 명령 추출
    public GameCommand GetCommand()
    {
        return DeserializeCommand(_payload);
    }

    // 배치 명령 추출
    public IEnumerable<GameCommand> GetCommands()
    {
        // TODO: 실제 역직렬화 구현
        yield break;
    }

    // 상태 스냅샷 추출
    public GameStateSnapshot GetStateSnapshot()
    {
        // TODO: 실제 역직렬화 구현
        return default;
    }

    // 핑 값 추출
    public int GetPing()
    {
        // TODO: 실제 역직렬화 구현
        return 0;
    }

    // 게임 참가 응답 추출
    public (bool success, ushort playerId, string errorMessage) GetJoinResponse()
    {
        // TODO: 실제 역직렬화 구현
        return (false, 0, "Not implemented");
    }

    #endregion
}

#endregion

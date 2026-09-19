using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if PHOTON_FUSION
using Fusion;
using Fusion.Sockets;
#endif

/// <summary>소유자에게서 개인 데이터를 수집하고 호스트 저장을 새 세션에 연결한다.</summary>
public static class NetworkSaveCoordinator
{
    public static async UniTask PrepareClientRestoreAsync(CancellationToken token = default)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || Main.Network.IsHost) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(180));
        int request = NextRequest();
        _joinRequest = request;
        _joinResult = null;
        _joinError = null;
        try
        {
            await UniTask.WaitUntil(() => Main.Network.LocalPlayerData != null,
                cancellationToken: timeout.Token);
            SendToHost(new Packet { kind = PacketKind.JoinRequest, request = request });
            await UniTask.WaitUntil(() => _joinResult != null || _joinError != null,
                cancellationToken: timeout.Token);
            if (_joinError != null) throw new InvalidOperationException(_joinError);
            Main.Save.SetRemoteLoad(_joinResult);
            var restored = Main.Save.GetLocalPlayerSave();
            if (restored != null) Main.Network.SetLocalCharacter(restored.characterIndex);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("호스트의 저장 월드 데이터를 받지 못했습니다. 다시 접속해 주세요.");
        }
        finally { _joinRequest = 0; }
#else
        await UniTask.CompletedTask;
#endif
    }

    public static async UniTask<List<PlayerSaveData>> CapturePlayersAsync(CancellationToken token = default)
    {
        if (Main.Network == null || !Main.Network.IsInRoom)
        {
            Player local = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
                if (candidate.IsLocalPlayer) { local = candidate; break; }
            if (local == null) throw new InvalidOperationException("저장할 플레이어가 없습니다.");
            await PlayerSaveAdapter.WaitUntilReadyAsync(local, token);
            await WorldItemSync.WaitForPendingAsync(token);
            return new List<PlayerSaveData> { PlayerSaveAdapter.Capture(local, Main.Save.LocalPlayerId,
                Main.Network != null ? Main.Network.LocalCharacterIndex : (int)PlayerCharacter.Female) };
        }
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || !Main.Network.IsHost)
            throw new InvalidOperationException("멀티플레이 저장은 호스트만 실행할 수 있습니다.");
        if (_captureRequest != 0) throw new InvalidOperationException("이미 플레이어 데이터를 수집 중입니다.");
        _captureRequest = NextRequest();
        _captureError = null;
        _expected.Clear();
        _captured.Clear();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(20));
        try
        {
            foreach (var pair in Main.Network.GetAllPlayers())
            {
                NetworkPlayerData data = pair.Value;
                if (data == null || string.IsNullOrEmpty(data.PersistentPlayerId.ToString()))
                    throw new InvalidOperationException("플레이어의 영구 ID가 준비되지 않았습니다.");
                if (!data.IsLoaded)
                    throw new InvalidOperationException("모든 플레이어가 로딩을 마친 뒤 저장해 주세요.");
                string id = data.PersistentPlayerId.ToString();
                if (_expected.ContainsValue(id))
                    throw new InvalidOperationException("같은 플레이어 ID가 중복 접속해 저장할 수 없습니다.");
                _expected.Add(pair.Key, id);
            }
            foreach (var pair in _expected)
            {
                if (pair.Key == Main.Network.LocalPlayer)
                {
                    Player local = GetLocalPlayer();
                    await PlayerSaveAdapter.WaitUntilReadyAsync(local, timeout.Token);
                    PlayerSaveData state = PlayerSaveAdapter.Capture(local, pair.Value, Main.Network.LocalCharacterIndex);
                    _captured.Add(pair.Key, state);
                }
                else
                {
                    SendToPlayer(pair.Key, new Packet { kind = PacketKind.CaptureRequest, request = _captureRequest });
                }
            }
            await UniTask.WaitUntil(() => _captureError != null || _captured.Count == _expected.Count,
                cancellationToken: timeout.Token);
            if (_captureError != null) throw new InvalidOperationException(_captureError);
            await WorldItemSync.WaitForPendingAsync(timeout.Token);
            // 다른 피어의 마지막 작업까지 호스트에 반영된 상태로 로컬 데이터를 다시 수집한다.
            _captured[Main.Network.LocalPlayer] = PlayerSaveAdapter.Capture(GetLocalPlayer(),
                Main.Save.LocalPlayerId, Main.Network.LocalCharacterIndex);
            // 이전 저장 이후 퇴장한 플레이어도 마지막으로 확인한 상태를 보존한다.
            var merged = new Dictionary<string, PlayerSaveData>(StringComparer.Ordinal);
            if (Main.Save.PendingLoad?.players != null)
                foreach (var state in Main.Save.PendingLoad.players) merged[state.playerId] = state;
            foreach (var pair in _remembered) merged[pair.Key] = pair.Value;
            foreach (var pair in _captured)
            {
                Remember(pair.Value);
                merged[pair.Value.playerId] = pair.Value;
            }
            return new List<PlayerSaveData>(merged.Values);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            EndCapture();
            throw new TimeoutException("일부 플레이어의 저장 응답이 없어 저장을 취소했습니다.");
        }
        catch { EndCapture(); throw; }
#else
        await UniTask.CompletedTask;
        throw new InvalidOperationException("네트워크 기능이 활성화되어 있지 않습니다.");
#endif
    }

    /// <summary>월드 캡처/파일 저장까지 끝난 뒤 SaveManager의 finally에서 호출한다.</summary>
    public static void EndCapture()
    {
#if PHOTON_FUSION
        if (_captureRequest != 0 && Main.Network != null && Main.Network.IsHost)
        {
            foreach (var pair in _expected)
            {
                if (pair.Key == Main.Network.LocalPlayer || Main.Network.GetPlayerData(pair.Key) == null) continue;
                try { SendToPlayer(pair.Key, new Packet { kind = PacketKind.ReleaseCapture, request = _captureRequest }); }
                catch (Exception error) { Debug.LogWarning($"저장 잠금 해제 전송 실패: {error.Message}"); }
            }
        }
        _captureRequest = 0;
        _expected.Clear();
        _captured.Clear();
#endif
    }

    public static async UniTask RestoreLocalPlayerAsync(Player player, CancellationToken token = default)
    {
        PlayerSaveData state = Main.Save.GetLocalPlayerSave();
        if (state != null) await PlayerSaveAdapter.RestoreAsync(player, state, token);
#if PHOTON_FUSION
        if (Main.Network != null && Main.Network.IsInRoom)
        {
            NetworkPlayerData local = Main.Network.LocalPlayerData;
            if (local == null) throw new InvalidOperationException("복원 완료를 보고할 플레이어 데이터가 없습니다.");
            local.Rpc_ReportSaveLoaded();
            if (state != null) Remember(state);
        }
#endif
    }

    /// <summary>정상 퇴장 시 개인 데이터를 호스트 메모리에 남긴 후 연결을 닫는다.</summary>
    public static async UniTask FlushLocalPlayerAsync(CancellationToken token = default)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || Main.Network.IsHost ||
            Main.Network.LocalPlayerData == null || !Main.Network.LocalPlayerData.IsLoaded || Main.Network.LocalCharacter == null) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _sessionCancellation.Token);
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(10));
        _departureRequest = NextRequest();
        _departureAcknowledged = false;
        Main.Save.SetCaptureBlocked(true);
        try
        {
            Player player = GetLocalPlayer();
            await PlayerSaveAdapter.WaitUntilReadyAsync(player, timeout.Token);
            await DrainItemRequestsAsync(timeout.Token);
            SendToHost(new Packet
            {
                kind = PacketKind.DepartureSnapshot, request = _departureRequest,
                player = PlayerSaveAdapter.Capture(player, Main.Save.LocalPlayerId, Main.Network.LocalCharacterIndex)
            });
            await UniTask.WaitUntil(() => _departureAcknowledged, cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("호스트가 퇴장 데이터를 확인하지 못했습니다. 다시 시도해 주세요.");
        }
        finally
        {
            _departureRequest = 0;
            if (_remoteCapture == 0) Main.Save.SetCaptureBlocked(false);
        }
#else
        await UniTask.CompletedTask;
#endif
    }

    public static void CaptureWorldState(WorldSaveData world)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom) return;
        if (!Main.Network.IsHost) throw new InvalidOperationException("호스트만 월드 저장 상태를 수집할 수 있습니다.");
        NetworkPlayerData master = FindMaster();
        if (master == null) throw new InvalidOperationException("호스트의 월드 상태가 준비되지 않았습니다.");
        world.network = master.CaptureNetworkSaveData();
        NetworkStructureDirector structures = NetworkStructureDirector.Instance;
        if (structures == null)
            throw new InvalidOperationException("호스트의 공유 건축물 상태가 준비되지 않았습니다.");
        world.structures = structures.CaptureSaveData();
#endif
    }

    public static void RestoreHostWorldState(WorldSaveData world)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || !Main.Network.IsHost || world?.network == null) return;
        NetworkPlayerData master = FindMaster();
        if (master == null) throw new InvalidOperationException("호스트의 월드 상태가 준비되지 않았습니다.");
        master.RestoreNetworkSaveData(world.network);
#endif
    }

    /// <summary>월드 준비 뒤 스폰된 구조물 디렉터에 저장된 공유 건축물을 복원한다.</summary>
    public static void RestoreHostStructures()
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || !Main.Network.IsHost ||
            Main.Save?.PendingLoad?.world == null || NetworkStructureDirector.Instance == null) return;
        NetworkStructureDirector.Instance.RestoreSaveData(Main.Save.PendingLoad.world.structures);
#endif
    }

    public static void RequestDropWithState(ItemStackSaveData state, Vector3 position)
    {
#if PHOTON_FUSION
        NetworkPlayerData local = Main.Network?.LocalPlayerData;
        if (local == null || state == null) throw new InvalidOperationException("아이템 드롭 상태를 전송할 수 없습니다.");
        local.Rpc_RequestDropItemState(state.itemId ?? string.Empty, state.itemKey, state.count,
            position, state.durability, state.spoilRemainingSeconds);
#endif
    }

#if PHOTON_FUSION
    private const int Protocol = 0x53415645;
    private const int MaxPacketBytes = 8 * 1024 * 1024;
    private enum PacketKind { CaptureRequest, PlayerSnapshot, ReleaseCapture, JoinRequest, JoinState, Error, DepartureSnapshot, DepartureAck }
    [Serializable] private class Packet
    {
        public PacketKind kind;
        public int request;
        public string error;
        public PlayerSaveData player;
        public GameSaveData game;
    }

    private static int _sequence;
    private static int _confirmedItemBarrier;

    public static void ConfirmItemBarrier(int request) => _confirmedItemBarrier = request;

    private static async UniTask DrainItemRequestsAsync(CancellationToken token)
    {
        // 첫 왕복은 기존 지급을, 두 번째는 지급 후 넘친 아이템의 재드롭을 확인한다.
        for (int pass = 0; pass < 2; pass++)
        {
            int request = NextRequest();
            Main.Network.LocalPlayerData.Rpc_RequestSaveBarrier(request);
            await UniTask.WaitUntil(() => _confirmedItemBarrier == request, cancellationToken: token);
            await WorldItemSync.WaitForPendingAsync(token);
        }
    }
    private static int _captureRequest;
    private static string _captureError;
    private static readonly Dictionary<PlayerRef, string> _expected = new();
    private static readonly Dictionary<PlayerRef, PlayerSaveData> _captured = new();
    private static readonly Dictionary<string, PlayerSaveData> _remembered = new(StringComparer.Ordinal);
    private static int _remoteCapture;
    private static int _joinRequest;
    private static int _departureRequest;
    private static bool _departureAcknowledged;
    private static GameSaveData _joinResult;
    private static string _joinError;
    private static CancellationTokenSource _sessionCancellation = new();

    private static int NextRequest()
    {
        _sequence = _sequence == int.MaxValue ? 1 : _sequence + 1;
        return _sequence;
    }

    private static Player GetLocalPlayer()
    {
        var character = Main.Network?.LocalCharacter;
        if (character == null || !character.TryGetComponent(out Player player))
            throw new InvalidOperationException("저장할 로컬 플레이어가 없습니다.");
        return player;
    }

    private static void Remember(PlayerSaveData player)
    {
        if (player == null || string.IsNullOrEmpty(player.playerId)) return;
        _remembered[player.playerId] = player;
        Main.Save.RememberPlayer(player);
    }

    public static PlayerSaveData FindSavedPlayer(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _remembered.TryGetValue(id, out var player) ? player : Main.Save.FindPlayer(id);
    }

    public static void OnPlayerLeft(PlayerRef player)
    {
        if (_captureRequest != 0 && _expected.ContainsKey(player))
            _captureError = "저장 중 플레이어가 연결을 종료해 저장을 취소했습니다.";
    }

    public static void OnSessionEnded()
    {
        _captureError = "네트워크 세션이 종료되었습니다.";
        _joinError = _captureError;
        _sessionCancellation.Cancel();
        _sessionCancellation.Dispose();
        _sessionCancellation = new CancellationTokenSource();
        _remoteCapture = 0;
        _captureRequest = 0;
        _expected.Clear();
        _captured.Clear();
        _remembered.Clear();
        Main.Save?.SetCaptureBlocked(false);
        Main.Save?.OnNetworkDisconnected();
    }

    private static void SendToPlayer(PlayerRef target, Packet packet)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
        if (bytes.Length > MaxPacketBytes) throw new InvalidOperationException("네트워크 저장 데이터가 전송 한도를 초과했습니다.");
        Main.Network.Runner.SendReliableDataToPlayer(target,
            ReliableKey.FromInts(Protocol, packet.request, (int)packet.kind, NextRequest()), bytes);
    }

    private static void SendToHost(Packet packet)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
        if (bytes.Length > MaxPacketBytes) throw new InvalidOperationException("플레이어 저장 데이터가 전송 한도를 초과했습니다.");
        Main.Network.Runner.SendReliableDataToServer(
            ReliableKey.FromInts(Protocol, packet.request, (int)packet.kind, NextRequest()), bytes);
    }

    public static void Receive(NetworkRunner runner, PlayerRef source, ReliableKey key, ArraySegment<byte> bytes)
    {
        key.GetInts(out int protocol, out _, out _, out _);
        if (protocol != Protocol || runner != Main.Network?.Runner) return;
        try
        {
            if (bytes.Array == null || bytes.Count <= 0 || bytes.Count > MaxPacketBytes)
                throw new InvalidOperationException("잘못된 저장 데이터 크기입니다.");
            Packet packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count));
            if (packet == null) return;
            if (runner.IsServer)
            {
                if (packet.kind == PacketKind.JoinRequest)
                    ReplyJoinAsync(source, packet.request, _sessionCancellation.Token).Forget();
                else if (packet.kind == PacketKind.PlayerSnapshot && packet.request == _captureRequest
                    && _expected.TryGetValue(source, out string expected))
                {
                    if (packet.player == null || packet.player.playerId != expected)
                        throw new InvalidOperationException("플레이어 저장 응답의 ID가 일치하지 않습니다.");
                    _captured[source] = packet.player;
                }
                else if (packet.kind == PacketKind.Error && packet.request == _captureRequest && _expected.ContainsKey(source))
                    _captureError = packet.error ?? "플레이어 데이터 수집에 실패했습니다.";
                else if (packet.kind == PacketKind.DepartureSnapshot)
                {
                    NetworkPlayerData owner = Main.Network.GetPlayerData(source);
                    if (owner == null || packet.player == null || packet.player.playerId != owner.PersistentPlayerId.ToString())
                        throw new InvalidOperationException("퇴장 플레이어의 저장 ID가 일치하지 않습니다.");
                    Remember(packet.player);
                    SendToPlayer(source, new Packet { kind = PacketKind.DepartureAck, request = packet.request });
                }
            }
            else
            {
                // Host 모드의 reliable server 전송만 수신한다.
                if (packet.kind == PacketKind.CaptureRequest)
                    ReplyCaptureAsync(packet.request, _sessionCancellation.Token).Forget();
                else if (packet.kind == PacketKind.ReleaseCapture && packet.request == _remoteCapture)
                {
                    _remoteCapture = 0;
                    Main.Save.SetCaptureBlocked(false);
                }
                else if (packet.kind == PacketKind.JoinState && packet.request == _joinRequest)
                    _joinResult = packet.game;
                else if (packet.kind == PacketKind.Error && packet.request == _joinRequest)
                    _joinError = packet.error ?? "호스트 데이터를 받지 못했습니다.";
                else if (packet.kind == PacketKind.DepartureAck && packet.request == _departureRequest)
                    _departureAcknowledged = true;
            }
        }
        catch (Exception error)
        {
            if (runner.IsServer) _captureError = error.Message;
            else _joinError = error.Message;
            Debug.LogException(error);
        }
    }

    private static async UniTaskVoid ReplyCaptureAsync(int request, CancellationToken token)
    {
        if (_remoteCapture != 0 && _remoteCapture != request)
        {
            SendToHost(new Packet { kind = PacketKind.Error, request = request, error = "다른 저장 요청을 처리 중입니다." });
            return;
        }
        _remoteCapture = request;
        Main.Save.SetCaptureBlocked(true);
        try
        {
            Player player = GetLocalPlayer();
            await PlayerSaveAdapter.WaitUntilReadyAsync(player, token);
            // 이미 처리 중인 프레임을 마친 뒤 입력이 잠긴 상태에서 순수 DTO를 만든다.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfterSlim(TimeSpan.FromSeconds(15));
            await DrainItemRequestsAsync(timeout.Token);
            PlayerSaveData state = PlayerSaveAdapter.Capture(player, Main.Save.LocalPlayerId, Main.Network.LocalCharacterIndex);
            SendToHost(new Packet { kind = PacketKind.PlayerSnapshot, request = request, player = state });
            // 호스트가 종료/예외로 release를 보내지 못해도 클라이언트를 영구 정지시키지 않는다.
            await UniTask.Delay(TimeSpan.FromSeconds(30), DelayType.UnscaledDeltaTime, cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (Main.Network != null && Main.Network.IsInRoom)
                SendToHost(new Packet { kind = PacketKind.Error, request = request, error = error.Message });
        }
        finally
        {
            if (_remoteCapture == request)
            {
                _remoteCapture = 0;
                Main.Save.SetCaptureBlocked(false);
            }
        }
    }

    private static async UniTaskVoid ReplyJoinAsync(PlayerRef source, int request, CancellationToken sessionToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        timeout.CancelAfterSlim(TimeSpan.FromSeconds(175));
        try
        {
            await UniTask.WaitUntil(() => Main.Network.GetPlayerData(source) == null ||
                Main.Network.GetPlayerData(source).ProfileRejected ||
                (!string.IsNullOrEmpty(Main.Network.GetPlayerData(source).PersistentPlayerId.ToString()) &&
                 (GameScene.GameState == GameState.Playing || GameScene.GameState == GameState.Failed) &&
                 !Main.Save.IsRestoring && !Main.Save.IsCapturing),
                cancellationToken: timeout.Token);
            NetworkPlayerData data = Main.Network.GetPlayerData(source);
            if (data == null) return;
            if (data.ProfileRejected) throw new InvalidOperationException("동일한 저장 프로필이 이미 접속 중입니다.");
            string id = data.PersistentPlayerId.ToString();
            var game = new GameSaveData
            {
                isMultiplayer = true, worldId = Main.Save.CurrentWorldId,
                savedAtUtc = DateTime.UtcNow.ToString("O"), world = WorldSaveAdapter.Capture()
            };
            PlayerSaveData saved = FindSavedPlayer(id);
            if (saved != null) game.players.Add(saved);
            SendToPlayer(source, new Packet { kind = PacketKind.JoinState, request = request, game = game });
        }
        catch (Exception error)
        {
            if (!sessionToken.IsCancellationRequested && Main.Network?.GetPlayerData(source) != null)
                SendToPlayer(source, new Packet { kind = PacketKind.Error, request = request, error = error.Message });
        }
    }

    private static NetworkPlayerData FindMaster()
    {
        if (Main.Network == null) return null;
        foreach (var pair in Main.Network.GetAllPlayers())
            if (pair.Value != null && pair.Value.IsMaster) return pair.Value;
        return null;
    }
#endif
}

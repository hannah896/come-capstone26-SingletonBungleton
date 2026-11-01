using System;
using UnityEngine;
#if PHOTON_FUSION
using System.Text;
using Fusion;
using Fusion.Sockets;
#endif

/// <summary>날씨는 호스트가 추첨하며 reliable 전송으로 현재 기간과 난수 상태를 전달합니다.</summary>
public static class WorldWeatherNetwork
{
    private static int _sequence;
    public static int NextRequest() => _sequence = _sequence == int.MaxValue ? 1 : _sequence + 1;

    public static void Request(int seed, int request)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || Main.Network.IsHost || Main.Network.Runner == null) return;
        byte[] bytes = Encode(new Packet { seed = seed, request = request });
        Main.Network.Runner.SendReliableDataToServer(ReliableKey.FromInts(Protocol, 0, request, NextRequest()), bytes);
#endif
    }

    public static void Broadcast(WorldWeatherSaveData snapshot)
    {
#if PHOTON_FUSION
        if (Main.Network == null || !Main.Network.IsInRoom || !Main.Network.IsHost) return;
        foreach (var pair in Main.Network.GetAllPlayers())
            if (pair.Key != Main.Network.LocalPlayer)
                Send(pair.Key, snapshot, 0);
#endif
    }

#if PHOTON_FUSION
    private const int Protocol = 0x57454154;
    private const int MaxBytes = 2 * 1024 * 1024;
    [Serializable]
    private sealed class Packet
    {
        public int seed;
        public int request;
        public WorldWeatherSaveData snapshot;
    }

    private static byte[] Encode(Packet packet)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
        if (bytes.Length > MaxBytes) throw new InvalidOperationException("날씨 동기화 데이터가 전송 한도를 초과했습니다.");
        return bytes;
    }

    private static void Send(PlayerRef player, WorldWeatherSaveData snapshot, int request)
    {
        Main.Network.Runner.SendReliableDataToPlayer(player,
            ReliableKey.FromInts(Protocol, 1, request, NextRequest()),
            Encode(new Packet { seed = snapshot.seed, request = request, snapshot = snapshot }));
    }

    public static void Receive(NetworkRunner runner, PlayerRef source, ReliableKey key, ArraySegment<byte> data)
    {
        key.GetInts(out int protocol, out int kind, out _, out _);
        if (protocol != Protocol || runner != Main.Network?.Runner || data.Array == null ||
            data.Count <= 0 || data.Count > MaxBytes) return;
        WorldWeatherController weather = WorldWeatherController.Instance;
        if (weather == null || !weather.IsInitialized) return;
        try
        {
            Packet packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(data.Array, data.Offset, data.Count));
            if (packet == null) return;
            if (runner.IsServer && kind == 0 && packet.snapshot == null &&
                Main.Network.GetPlayerData(source) != null)
            {
                WorldWeatherSaveData snapshot = weather.Capture();
                if (packet.seed == snapshot.seed) Send(source, snapshot, packet.request);
            }
            else if (!runner.IsServer && kind == 1 && packet.snapshot != null)
            {
                weather.ReceiveNetworkSnapshot(packet.snapshot, packet.request);
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[WorldWeather] 날씨 상태를 수신하지 못했습니다: {error.Message}");
        }
    }
#endif
}

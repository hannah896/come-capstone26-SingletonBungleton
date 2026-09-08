using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 멀티플레이에서 다른 플레이어가 접속하면 토스트로 알린다.
/// GameScene이 멀티 세션일 때만 동적으로 생성한다.
///
/// [이름을 기다리는 이유]
/// 호스트가 스폰한 NetworkPlayerData가 복제 도착하는 시점(= 입장 감지 시점)에는 아직 이름이 비어 있다.
/// 클라이언트가 자기 Spawned() 이후에 Rpc_SetProfile로 이름을 올리기 때문이다.
/// 그래서 입장을 감지해도 바로 알리지 않고, 이름이 채워진 갱신을 기다렸다가 알린다.
/// 이름이 끝내 오지 않으면(구버전 클라·패킷 유실 등) <see cref="NameWaitTimeout"/> 뒤 기본 이름으로 알린다.
/// </summary>
public class NetworkPlayerJoinNotice : MonoBehaviour
{
    #region Fields

    // 이름이 도착하기를 기다리는 최대 시간(초). 넘기면 "플레이어 N"으로 알린다.
    private const float NameWaitTimeout = 3f;

    // 토스트 표시 시간(초)
    private const float ToastDuration = 2.5f;

    // 이미 알린(또는 알릴 필요가 없는) 플레이어들
    private readonly HashSet<int> _announced = new();

    // 이름을 기다리는 중인 플레이어 → 대기 시작 시각
    private readonly Dictionary<int, float> _waitingForName = new();

    // 타임아웃 처리 중 컬렉션 수정을 피하기 위한 임시 버퍼
    private readonly List<int> _timedOut = new();

    private bool _subscribed;

    #endregion

    #region Lifecycle

    private void Start()
    {
        // 싱글플레이거나 네트워크가 없으면 할 일이 없다
        if (Main.Network == null || !Main.Network.IsInRoom)
        {
            Destroy(gameObject);
            return;
        }

        // 내가 들어왔을 때 이미 방에 있던 사람은 "접속했다"고 알리지 않는다
        foreach (WaitingPlayerInfo player in Main.Network.GetWaitingPlayers())
        {
            _announced.Add(player.PlayerId);
        }

        Main.Network.OnWaitingPlayersChanged += OnPlayersChanged;
        Main.Loop.OnUpdate += OnLoopUpdate;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (!_subscribed) return;

        if (Main.Instance != null)
        {
            if (Main.Network != null) Main.Network.OnWaitingPlayersChanged -= OnPlayersChanged;
            if (Main.Loop != null) Main.Loop.OnUpdate -= OnLoopUpdate;
        }

        _subscribed = false;
    }

    #endregion

    #region 입장 감지

    // 입장/퇴장/이름·성별 변경 시 호출된다 (NetworkManager가 전체 리빌드를 알리는 이벤트)
    private void OnPlayersChanged()
    {
        if (Main.Network == null) return;

        List<WaitingPlayerInfo> players = Main.Network.GetWaitingPlayers();

        var present = new HashSet<int>();
        foreach (WaitingPlayerInfo player in players)
        {
            present.Add(player.PlayerId);

            if (_announced.Contains(player.PlayerId)) continue;

            // 나 자신의 입장은 알리지 않는다
            if (player.IsLocal)
            {
                _announced.Add(player.PlayerId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(player.PlayerName))
            {
                // 이름이 아직 복제되지 않았다 → 다음 갱신을 기다린다
                if (!_waitingForName.ContainsKey(player.PlayerId))
                    _waitingForName[player.PlayerId] = Time.unscaledTime;
                continue;
            }

            Announce(player.PlayerName);
            _announced.Add(player.PlayerId);
            _waitingForName.Remove(player.PlayerId);
        }

        // 나간 사람은 기록에서 지운다 (다시 들어오면 그때 새로 알린다)
        _announced.RemoveWhere(id => !present.Contains(id));

        _timedOut.Clear();
        foreach (int id in _waitingForName.Keys)
        {
            if (!present.Contains(id)) _timedOut.Add(id);
        }
        foreach (int id in _timedOut) _waitingForName.Remove(id);
    }

    // 이름이 끝내 도착하지 않은 플레이어를 기본 이름으로 알린다
    private void OnLoopUpdate(float deltaTime)
    {
        if (_waitingForName.Count == 0) return;

        float now = Time.unscaledTime;

        _timedOut.Clear();
        foreach (KeyValuePair<int, float> pair in _waitingForName)
        {
            if (now - pair.Value >= NameWaitTimeout) _timedOut.Add(pair.Key);
        }

        foreach (int id in _timedOut)
        {
            _waitingForName.Remove(id);

            if (!_announced.Add(id)) continue;

            Announce($"플레이어 {id}");
        }
    }

    private void Announce(string playerName)
    {
        Toast.Show($"{playerName}님이 접속했습니다", ToastDuration, ToastColor.Green, ToastPosition.TopCenter);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkPlayerJoinNotice] {playerName} joined");
#endif
    }

    #endregion
}

#if PHOTON_FUSION
using System.Text;
using Fusion;
using UnityEngine;

/// <summary>
/// 네트워크에서 동기화되는 플레이어 데이터.
/// Host 모드: 호스트가 모든 플레이어의 데이터 오브젝트를 스폰하며 StateAuthority를 보유한다.
/// 각 클라이언트는 자기 오브젝트에 InputAuthority만 가지며,
/// 이름/성별 등 자기 값 변경은 RPC로 호스트에 요청하고 결과는 Networked 복제로 받는다.
/// </summary>
public partial class NetworkPlayerData : NetworkBehaviour
{
    #region Networked Properties

    // 이 데이터를 소유한 플레이어 참조
    [Networked] public PlayerRef OwnerRef { get; set; }

    // 세션의 PlayerRef와 별개로 앱 재실행 후에도 유지되는 저장 프로필 ID.
    [Networked] public NetworkString<_64> PersistentPlayerId { get; set; }
    [Networked] public NetworkBool ProfileRejected { get; set; }

    // 플레이어 이름 (바이트 버퍼로 네트워크 동기화)
    [Networked, Capacity(32)] public NetworkArray<byte> NameBuffer => default;

    // 준비 완료 여부
    [Networked] public NetworkBool IsReady { get; set; }

    // 씬 로딩 완료 여부
    [Networked] public NetworkBool IsLoaded { get; set; }

    // 팀 인덱스
    [Networked] public int TeamIndex { get; set; }

    // 외형 색상 인덱스
    [Networked] public int ColorIndex { get; set; }

    // 캐릭터 성별 인덱스 (PlayerCharacter 값 — 대기방 목록 표시 + 호스트의 캐릭터 프리팹 분기용)
    [Networked] public int CharacterIndex { get; set; }

    // 방장 여부 — Host 모드에서는 호스트가 곧 방장이며, 스폰 시 호스트가 기록한다
    [Networked] public NetworkBool IsMaster { get; set; }

    // 호스트가 확정한 월드 생성 옵션 (스폰 시 호스트가 기록 → 복제로 클라에 도착).
    // 세션 속성(SessionInfo.Properties)은 방 참가 직후 아직 비어 있을 수 있어,
    // 시드가 확실히 전달되도록 복제 상태로도 함께 내려보낸다. (NetworkWorldConfig 참고)
    [Networked] public int WorldSeed { get; set; }
    [Networked] public int WorldBranch { get; set; }
    [Networked] public int WorldLoop { get; set; }
    [Networked] public int WorldSizeIndex { get; set; }
    [Networked] public NetworkBool HasWorldConfig { get; set; }

    // 월드 시계 (호스트 자신의 데이터 오브젝트에만 기록 → 모든 클라가 읽어 자기 WorldClock에 반영).
    // 누적 초 하나로 보내면 날이 지날수록 float 정밀도가 떨어지므로 날짜와 하루 중 경과 초로 나눈다.
    [Networked] public int WorldClockDays { get; set; }
    [Networked] public float WorldClockSecondsToday { get; set; }
    [Networked] public NetworkBool HasWorldClock { get; set; }

    // 월드 시간 배속 (에디터 Time Debug 툴 — 누가 바꾸든 호스트 값으로 전원 동일)
    [Networked] public float WorldClockTimeScale { get; set; }

    #endregion

    #region Fields

    // Networked 프로퍼티 변경 감지 (대기방 UI 갱신용)
    private ChangeDetector _changes;

    // 이 오브젝트가 로컬 WorldClock을 네트워크 구동 중으로 전환했는지 (Despawned에서 되돌리기용)
    private bool _drivingWorldClock;

    // 클라이언트: 마지막으로 반영한 호스트 배속 (값이 바뀔 때만 반영해, 요청 직후 표시가 옛 값으로 튀지 않게)
    private float _appliedTimeScale = -1f;
    private WorldClock _appliedTimeScaleClock;

    #endregion

    #region Properties

    // NameBuffer를 문자열로 변환하여 반환
    public string PlayerName => GetName();

    // 로컬 플레이어 소유 여부 (자기 데이터 오브젝트에는 InputAuthority가 부여된다)
    public bool IsLocal => Runner != null && Runner.LocalPlayer == OwnerRef;

    #endregion

    #region Lifecycle

    public override void Spawned()
    {
        // 씬 전환(Additive 로드 + 이전 씬 언로드)에서 살아남도록 러너처럼 유지
        DontDestroyOnLoad(gameObject);

        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        Main.Network?.RegisterPlayerData(OwnerRef, this);

        // 방장 데이터면 월드 공유 상태(자원 파괴/바닥 아이템)의 창구가 된다
        InitializeWorldState();

        // 클라이언트: 자기 데이터가 복제 도착하면 로컬에서 고른 이름/성별을 호스트에 올린다
        // (호스트 자신의 값은 스폰 시 onBeforeSpawned에서 이미 기록됨)
        if (HasInputAuthority && !HasStateAuthority && Main.Network != null)
        {
            Rpc_SetProfile(Main.Network.LocalPlayerName, Main.Network.LocalCharacterIndex, Main.Save.LocalPlayerId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkPlayerData] Spawned for player: {OwnerRef}");
#endif
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // 시간을 구동하던 호스트 데이터가 사라지면(세션 종료) 로컬 시계 흐름을 되돌린다.
        if (_drivingWorldClock && WorldClock.Instance != null)
            WorldClock.Instance.SetNetworkDriven(false);
        _drivingWorldClock = false;

        ReleaseWorldState();

        Main.Network?.UnregisterPlayerData(OwnerRef);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkPlayerData] Despawned for player: {OwnerRef}");
#endif
    }

    // 호스트: 자기 데이터 오브젝트에서 틱마다 월드 시간을 전진시키고 결과를 복제 상태에 기록한다.
    // TimeManager(NyoTimer)는 GameState가 Playing일 때만 흐르므로, 호스트가 죽거나 일시정지해도
    // 세션 시간이 멈추지 않도록 네트워크 틱으로 직접 구동한다.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsMaster) return;
        if (Main.Save != null && (Main.Save.IsCapturing || Main.Save.IsRestoring)) return;

        TickWorldStateHost();

        WorldClock clock = WorldClock.Instance;
        if (clock == null) return;

        // 맵/플레이어 생성 중에는 싱글과 마찬가지로 시간을 흘리지 않는다 (사망 중에는 계속 흐른다).
        GameState state = GameScene.GameState;
        bool canAdvance = state == GameState.Playing || state == GameState.Failed;

        if (!clock.IsNetworkDriven) clock.SetNetworkDriven(true);
        _drivingWorldClock = true;

        if (canAdvance)
            clock.SyncTime(clock.DaysPassed, clock.ElapsedSecondsToday + Runner.DeltaTime * clock.TimeScale);

        // SkipTime 등 호스트 로컬 변경도 그대로 반영된다.
        WorldClockDays = clock.DaysPassed;
        WorldClockSecondsToday = clock.ElapsedSecondsToday;
        WorldClockTimeScale = clock.TimeScale;
        HasWorldClock = true;
    }

    // Fusion 렌더 콜백 — 대기방 표시 값이 바뀌면 매니저에 알린다 (호스트 쓰기/원격 복제 모두 감지)
    public override void Render()
    {
        ApplyWorldClockFromHost();
        RenderWorldState();

        if (_changes == null) return;

        foreach (string propertyName in _changes.DetectChanges(this))
        {
            switch (propertyName)
            {
                case nameof(CharacterIndex):
                case nameof(NameBuffer):
                case nameof(IsMaster):
                    Main.Network?.NotifyPlayerDataChanged(this);
                    return; // 대기방 UI는 전체 리빌드라 프레임당 1회면 충분
            }
        }
    }

    // 클라이언트: 호스트 데이터에 복제된 월드 시간을 자기 WorldClock에 그대로 반영한다.
    private void ApplyWorldClockFromHost()
    {
        if (HasStateAuthority || !IsMaster || !HasWorldClock) return;

        WorldClock clock = WorldClock.Instance;
        if (clock == null) return; // 아직 월드 생성 전 — 시계가 생기면 다음 프레임부터 반영

        if (!clock.IsNetworkDriven) clock.SetNetworkDriven(true);
        _drivingWorldClock = true;

        clock.SyncTime(WorldClockDays, WorldClockSecondsToday);

        // 시계가 새로 만들어졌거나(기본 x1) 호스트 배속이 바뀌었을 때만 반영
        if (clock != _appliedTimeScaleClock || WorldClockTimeScale != _appliedTimeScale)
        {
            _appliedTimeScaleClock = clock;
            _appliedTimeScale = WorldClockTimeScale;
            clock.SyncTimeScale(WorldClockTimeScale);
        }
    }

    #endregion

    #region RPC

    /// <summary>
    /// 클라이언트가 시간 스킵(수면 등)을 호스트에 요청합니다. 결과는 호스트 시계 복제로 전원에게 반영됩니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestSkipTime(float skipSeconds)
    {
        WorldClock clock = WorldClock.Instance;
        if (clock == null || skipSeconds <= 0f) return;

        clock.SkipTime(skipSeconds);
    }

    /// <summary>
    /// 클라이언트가 월드 시간 배속 변경을 호스트에 요청합니다. (에디터 Time Debug 툴) 결과는 복제로 전원에게 반영됩니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestTimeScale(float scale)
    {
        WorldClock clock = WorldClock.Instance;
        if (clock == null) return;

        clock.SetTimeScale(scale);
    }

    /// <summary>
    /// 게임 시작 신호. 호스트(StateAuthority)가 자기 데이터 오브젝트에서 호출하면
    /// 전 클라이언트(본인 포함)가 수신합니다.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_StartGame()
    {
        Main.Network?.HandleGameStartReceived();
    }

    /// <summary>
    /// 클라이언트가 자기 이름/성별을 호스트에 등록합니다. (스폰 직후 1회)
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetProfile(string playerName, int characterIndex, string persistentPlayerId)
    {
        if (!System.Guid.TryParse(persistentPlayerId, out var parsed)) { ProfileRejected = true; return; }
        string normalized = parsed.ToString("N");
        foreach (var pair in Main.Network.GetAllPlayers())
        {
            if (pair.Key != OwnerRef && pair.Value != null && pair.Value.PersistentPlayerId.ToString() == normalized)
            {
                ProfileRejected = true;
                Debug.LogError("동일한 저장 플레이어 ID의 중복 접속을 거부했습니다.");
                return;
            }
        }
        PersistentPlayerId = normalized;
        WriteNameInternal(playerName);
        PlayerSaveData saved = NetworkSaveCoordinator.FindSavedPlayer(normalized);
        CharacterIndex = saved != null ? saved.characterIndex : characterIndex;
    }

    /// <summary>
    /// 클라이언트가 "내 월드 생성이 끝났다"고 호스트에 보고합니다.
    ///
    /// 각 피어는 같은 시드로 자기 월드를 따로 생성하므로, 호스트가 이 보고를 받기 전에 캐릭터를 스폰하면
    /// 그 피어에는 아직 지형이 없어 캐릭터가 끝없이 아래로 떨어진다.
    /// 호스트는 이 보고를 받은 뒤에 해당 플레이어의 캐릭터를 스폰한다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_ReportWorldReady()
    {
        Main.Network?.HandleWorldReadyReported(OwnerRef);
    }

    /// <summary>
    /// 클라이언트가 이름 변경을 호스트에 요청합니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetName(string playerName)
    {
        WriteNameInternal(playerName);
    }

    /// <summary>
    /// 클라이언트가 캐릭터 성별 변경을 호스트에 요청합니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SetCharacter(int characterIndex)
    {
        CharacterIndex = characterIndex;
    }

    /// <summary>
    /// 채팅 메시지를 전송합니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void Rpc_SendChatMessage(byte[] message)
    {
        var text = Encoding.UTF8.GetString(message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Chat] Player {OwnerRef}: {text}");
#endif
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 플레이어 이름을 설정합니다.
    /// 호스트는 직접 쓰고, 클라이언트는 자기 데이터(InputAuthority)에 한해 RPC로 요청합니다.
    /// </summary>
    public void SetName(string name)
    {
        if (HasStateAuthority)
        {
            WriteNameInternal(name);
        }
        else if (HasInputAuthority)
        {
            Rpc_SetName(name);
        }
    }

    /// <summary>
    /// 캐릭터 성별을 설정합니다.
    /// 호스트는 직접 쓰고, 클라이언트는 자기 데이터(InputAuthority)에 한해 RPC로 요청합니다.
    /// </summary>
    public void SetCharacter(int characterIndex)
    {
        if (HasStateAuthority)
        {
            CharacterIndex = characterIndex;
        }
        else if (HasInputAuthority)
        {
            Rpc_SetCharacter(characterIndex);
        }
    }

    /// <summary>
    /// NameBuffer에 문자열을 기록합니다. (StateAuthority 전용 — onBeforeSpawned 선기록에서도 사용)
    /// </summary>
    public void WriteNameInternal(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name ?? string.Empty);

        for (int i = 0; i < NameBuffer.Length; i++)
        {
            NameBuffer.Set(i, i < bytes.Length ? bytes[i] : (byte)0);
        }
    }

    // NameBuffer에서 문자열로 변환
    public string GetName()
    {
        var bytes = new byte[NameBuffer.Length];
        int length = 0;

        for (int i = 0; i < NameBuffer.Length; i++)
        {
            byte b = NameBuffer[i];
            if (b == 0) break;
            bytes[i] = b;
            length++;
        }

        return length > 0 ? Encoding.UTF8.GetString(bytes, 0, length) : string.Empty;
    }

    #endregion
}
#endif

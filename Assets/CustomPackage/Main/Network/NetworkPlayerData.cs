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
public class NetworkPlayerData : NetworkBehaviour
{
    #region Networked Properties

    // 이 데이터를 소유한 플레이어 참조
    [Networked] public PlayerRef OwnerRef { get; set; }

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

    #endregion

    #region Fields

    // Networked 프로퍼티 변경 감지 (대기방 UI 갱신용)
    private ChangeDetector _changes;

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

        // 클라이언트: 자기 데이터가 복제 도착하면 로컬에서 고른 이름/성별을 호스트에 올린다
        // (호스트 자신의 값은 스폰 시 onBeforeSpawned에서 이미 기록됨)
        if (HasInputAuthority && !HasStateAuthority && Main.Network != null)
        {
            Rpc_SetProfile(Main.Network.LocalPlayerName, Main.Network.LocalCharacterIndex);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkPlayerData] Spawned for player: {OwnerRef}");
#endif
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Main.Network?.UnregisterPlayerData(OwnerRef);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NetworkPlayerData] Despawned for player: {OwnerRef}");
#endif
    }

    // Fusion 렌더 콜백 — 대기방 표시 값이 바뀌면 매니저에 알린다 (호스트 쓰기/원격 복제 모두 감지)
    public override void Render()
    {
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

    #endregion

    #region RPC

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
    private void Rpc_SetProfile(string playerName, int characterIndex)
    {
        WriteNameInternal(playerName);
        CharacterIndex = characterIndex;
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

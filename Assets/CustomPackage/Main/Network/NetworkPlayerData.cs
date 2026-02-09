#if PHOTON_FUSION
using System.Text;
using Fusion;
using UnityEngine;

/// <summary>
/// 네트워크에서 동기화되는 플레이어 데이터.
/// 각 플레이어마다 하나씩 스폰되며, Networked 프로퍼티를 통해 자동 동기화됩니다.
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

    #endregion

    #region Properties

    // NameBuffer를 문자열로 변환하여 반환
    public string PlayerName => GetName();

    // 로컬 플레이어 소유 여부
    public bool IsLocal => Runner.LocalPlayer == OwnerRef;

    #endregion

    #region Lifecycle

    public override void Spawned()
    {
        Main.Network?.RegisterPlayerData(OwnerRef, this);

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

    #endregion

    #region RPC

    /// <summary>
    /// 플레이어 이름을 동기화합니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetName(byte[] nameBytes)
    {
        for (int i = 0; i < NameBuffer.Length; i++)
        {
            NameBuffer.Set(i, i < nameBytes.Length ? nameBytes[i] : (byte)0);
        }
    }

    /// <summary>
    /// 준비 상태를 동기화합니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReady(NetworkBool isReady)
    {
        IsReady = isReady;
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
    /// 플레이어 이름을 설정합니다. InputAuthority를 가진 클라이언트에서 호출합니다.
    /// </summary>
    public void SetName(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        Rpc_SetName(bytes);
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

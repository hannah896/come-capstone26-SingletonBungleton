#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>호스트가 확정한 자원 파괴 1건 (키 = 배치 오브젝트 instanceId).</summary>
public struct WorldDestroyedEntry : INetworkStruct
{
    // 다시 생기는 인게임 시각 (WorldClock.TotalInGameSeconds 기준)
    public float RespawnTime;

    // 마지막 타격으로 부순 플레이어 — 이 피어만 드롭/아이템을 받는다
    public PlayerRef Breaker;
}

/// <summary>아직 부서지지 않은 자원의 누적 데미지 (키 = 배치 오브젝트 instanceId).</summary>
public struct WorldDamageEntry : INetworkStruct
{
    public int Damage;

    // 마지막으로 맞은 네트워크 시각 (Runner.SimulationTime) — 오래 방치된 항목 정리용
    public float LastHitTime;
}

/// <summary>바닥에 떨어진 아이템 1묶음 (키 = 호스트가 발급한 드롭 ID).</summary>
public struct WorldDropEntry : INetworkStruct
{
    // 아이템 Addressable 키(= ItemDataSO 이름)의 해시. 실제 키는 방장 데이터의 ItemKeyNames에서 찾는다.
    // (문자열을 그대로 넣으면 한 칸이 수십 워드라 오브젝트 상태 상한 32KB를 넘는다 — NetworkItemKeys 참고)
    public int ItemHash;
    public int Count;
    public Vector3 Position;
}

/// <summary>
/// 월드 공유 상태의 Fusion 구현. 실제 복제 데이터는 방장 <see cref="NetworkPlayerData"/>에 있고,
/// 이 클래스는 월드/아이템 코드(<see cref="WorldResourceSync"/>, <see cref="WorldItemSync"/>)가 보는 창구다.
///
/// - 호스트: 방장 데이터에 바로 기록
/// - 클라: 자기 데이터 오브젝트의 RPC로 호스트에 요청 (InputAuthority → StateAuthority)
/// </summary>
public sealed class NetworkWorldState : IWorldStateNetwork
{
    private readonly NetworkPlayerData _master;

    public NetworkWorldState(NetworkPlayerData master)
    {
        _master = master;
    }

    public bool IsActive => _master != null && _master.Object != null && _master.Object.IsValid;

    public GameObject LocalPlayerObject
    {
        get
        {
            NetworkObject character = Main.Network != null ? Main.Network.LocalCharacter : null;
            return character != null ? character.gameObject : null;
        }
    }

    public int GetResourceDamage(int instanceId)
    {
        if (!IsActive) return 0;
        return _master.ResourceDamages.TryGet(instanceId, out WorldDamageEntry entry) ? entry.Damage : 0;
    }

    public void ReportResourceDamage(int instanceId, int amount, int maxHealth, float respawnSeconds)
    {
        if (!TryGetSender(out NetworkPlayerData local)) return;

        if (local.HasStateAuthority)
            _master.HostReportDamage(local.OwnerRef, instanceId, amount, maxHealth, respawnSeconds);
        else
            local.Rpc_ReportResourceDamage(instanceId, amount, maxHealth, respawnSeconds);
    }

    public void RequestDropItem(string itemKey, int count, Vector3 position)
    {
        if (!TryGetSender(out NetworkPlayerData local)) return;

        if (local.HasStateAuthority)
            _master.HostDropItem(itemKey, count, position);
        else
            local.Rpc_RequestDropItem(itemKey, count, position);
    }

    public void RequestPickupItem(int dropId, int amount)
    {
        if (!TryGetSender(out NetworkPlayerData local)) return;

        if (local.HasStateAuthority)
            _master.HostPickupItem(local.OwnerRef, dropId, amount);
        else
            local.Rpc_RequestPickupItem(dropId, amount);
    }

    public void RemoveDrop(int dropId)
    {
        if (!IsActive || !_master.HasStateAuthority) return;
        _master.HostRemoveDrop(dropId);
    }

    // 요청을 보낼 이 피어의 데이터 오브젝트 (클라는 자기 것에만 InputAuthority가 있다)
    private bool TryGetSender(out NetworkPlayerData local)
    {
        local = IsActive && Main.Network != null ? Main.Network.LocalPlayerData : null;
        if (local != null && local.Object != null && local.Object.IsValid) return true;

        Debug.LogWarning("[NetworkWorldState] 로컬 플레이어 데이터가 없어 월드 상태 요청을 보내지 못했습니다.");
        return false;
    }
}
#endif

#if PHOTON_FUSION
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// 월드 공유 상태 (자원 누적 데미지 / 자원 파괴 / 바닥 아이템).
///
/// 세션 전역 상태라 방장 데이터 오브젝트에만 기록하고 모든 피어가 읽는다. (WorldClock과 같은 방식)
/// 월드 자체는 각 피어가 같은 시드로 따로 생성하므로, 배치 오브젝트는 instanceId만으로 가리킨다.
///
/// 흐름:
///   요청(데미지/드롭/줍기) → 호스트가 테이블 갱신 → 복제 → 각 피어 Render에서 차이만 자기 월드에 반영
/// </summary>
public partial class NetworkPlayerData
{
    #region Constants

    private const int DestroyedCapacity = 512;
    private const int DamageCapacity = 128;
    private const int DropCapacity = 256;

    // 이 시간(현실 초) 동안 아무도 안 친 자원은 누적 데미지를 잊는다 (용량 확보)
    private const float DamageForgetSeconds = 60f;

    // 호스트 정리 주기 (현실 초)
    private const float PruneInterval = 1f;

    #endregion

    #region Networked (방장 데이터에서만 사용)

    [Networked, Capacity(DestroyedCapacity)]
    public NetworkDictionary<int, WorldDestroyedEntry> DestroyedResources => default;

    [Networked, Capacity(DamageCapacity)]
    public NetworkDictionary<int, WorldDamageEntry> ResourceDamages => default;

    [Networked, Capacity(DropCapacity)]
    public NetworkDictionary<int, WorldDropEntry> WorldDrops => default;

    [Networked] private int LastDropId { get; set; }

    #endregion

    #region Fields

    // 이 피어에서 월드 상태 반영을 맡은 방장 데이터 (세션당 하나)
    private NetworkWorldState _worldState;
    private ChangeDetector _worldChanges;

    // 반영을 마친 월드 (월드가 새로 만들어지면 전체를 다시 반영한다)
    private object _appliedWorld;
    private bool _worldDirty;

    // 이미 반영한 파괴 항목 (instanceId → 재생성 시각)
    private readonly Dictionary<int, float> _appliedDestroyed = new();

    // 이 피어에 만들어 둔 바닥 아이템 (드롭 ID → 표시 오브젝트)
    private readonly Dictionary<int, DropView> _dropViews = new();

    private readonly HashSet<int> _seenIds = new();
    private readonly List<int> _removeBuffer = new();

    private float _nextPruneTime;

    private sealed class DropView
    {
        public Item Item;
        public int Count;
        public bool Removed;
    }

    #endregion

    #region Lifecycle (NetworkPlayerData에서 호출)

    private void InitializeWorldState()
    {
        if (!IsMaster) return;

        _worldState = new NetworkWorldState(this);
        _worldChanges = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _appliedWorld = null;
        WorldResourceSync.Bind(_worldState);
    }

    private void ReleaseWorldState()
    {
        if (_worldState == null) return;

        WorldResourceSync.Unbind(_worldState);
        _worldState = null;
        _appliedWorld = null;
        _appliedDestroyed.Clear();
        ClearDropViews();
    }

    // 호스트: 재생성 시각이 지난 파괴 항목과 오래 방치된 누적 데미지를 지운다
    private void TickWorldStateHost()
    {
        if (_worldState == null) return;

        float now = Runner.SimulationTime;
        if (now < _nextPruneTime) return;
        _nextPruneTime = now + PruneInterval;

        _removeBuffer.Clear();
        foreach (KeyValuePair<int, WorldDamageEntry> pair in ResourceDamages)
        {
            if (now - pair.Value.LastHitTime >= DamageForgetSeconds)
                _removeBuffer.Add(pair.Key);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
            ResourceDamages.Remove(_removeBuffer[i]);

        WorldClock clock = WorldClock.Instance;
        if (clock == null) return;

        float gameTime = clock.TotalInGameSeconds;
        _removeBuffer.Clear();
        foreach (KeyValuePair<int, WorldDestroyedEntry> pair in DestroyedResources)
        {
            if (gameTime >= pair.Value.RespawnTime)
                _removeBuffer.Add(pair.Key);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
            DestroyedResources.Remove(_removeBuffer[i]);
    }

    // 모든 피어: 복제된 테이블과 자기 월드의 차이를 반영한다
    private void RenderWorldState()
    {
        if (_worldState == null || _worldChanges == null) return;

        foreach (string propertyName in _worldChanges.DetectChanges(this))
        {
            if (propertyName == nameof(DestroyedResources) || propertyName == nameof(WorldDrops))
                _worldDirty = true;
        }

        // 월드 생성 전이면 기다렸다가, 준비되는 순간 밀린 상태를 한꺼번에 반영한다
        object world = WorldResourceSync.CurrentWorld;
        if (world == null) return;

        bool initialSync = !ReferenceEquals(world, _appliedWorld);
        if (initialSync)
        {
            _appliedWorld = world;
            _appliedDestroyed.Clear();
            ClearDropViews();
            _worldDirty = true;
        }

        if (!_worldDirty) return;
        _worldDirty = false;

        SyncDestroyedResources(initialSync);
        SyncWorldDrops();
    }

    #endregion

    #region 반영 (모든 피어)

    private void SyncDestroyedResources(bool initialSync)
    {
        PlayerRef localPlayer = Runner.LocalPlayer;
        bool hasPending = false;

        _seenIds.Clear();
        foreach (KeyValuePair<int, WorldDestroyedEntry> pair in DestroyedResources)
        {
            _seenIds.Add(pair.Key);

            if (_appliedDestroyed.TryGetValue(pair.Key, out float appliedTime) && appliedTime == pair.Value.RespawnTime)
                continue;

            // 뒤늦게 월드가 준비돼 몰아서 반영할 때는 예전에 부순 것까지 드롭을 주지 않는다
            bool destroyedByLocal = !initialSync && pair.Value.Breaker == localPlayer;

            if (!WorldResourceSync.ApplyDestroyed(pair.Key, pair.Value.RespawnTime, destroyedByLocal))
            {
                hasPending = true;
                continue;
            }

            _appliedDestroyed[pair.Key] = pair.Value.RespawnTime;
        }

        // 호스트가 지운 항목 (재생성 시각이 지남) — 실제 재생성은 각 피어의 WorldSimulationManager가 처리
        _removeBuffer.Clear();
        foreach (int id in _appliedDestroyed.Keys)
        {
            if (!_seenIds.Contains(id)) _removeBuffer.Add(id);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
            _appliedDestroyed.Remove(_removeBuffer[i]);

        if (hasPending) _worldDirty = true;
    }

    private void SyncWorldDrops()
    {
        _seenIds.Clear();
        foreach (KeyValuePair<int, WorldDropEntry> pair in WorldDrops)
        {
            _seenIds.Add(pair.Key);
            WorldDropEntry entry = pair.Value;

            if (_dropViews.TryGetValue(pair.Key, out DropView view))
            {
                if (view.Count != entry.Count)
                {
                    view.Count = entry.Count;
                    if (view.Item != null) WorldItemSync.SetDropCount(view.Item, entry.Count);
                }
                continue;
            }

            view = new DropView { Count = entry.Count };
            _dropViews[pair.Key] = view;
            SpawnDropViewAsync(pair.Key, entry.ItemKey.ToString(), entry.Position, view).Forget();
        }

        // 누군가 주워서 사라진 드롭
        _removeBuffer.Clear();
        foreach (int id in _dropViews.Keys)
        {
            if (!_seenIds.Contains(id)) _removeBuffer.Add(id);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            int id = _removeBuffer[i];
            RemoveDropView(_dropViews[id]);
            _dropViews.Remove(id);
        }
    }

    private async UniTaskVoid SpawnDropViewAsync(int dropId, string itemKey, Vector3 position, DropView view)
    {
        Item item = await WorldItemSync.SpawnItemAsync(itemKey, view.Count, position);
        if (item == null) return;

        // 로드하는 사이 주워졌거나 세션이 끝났다
        if (view.Removed)
        {
            Extensions.Despawn(item.gameObject);
            return;
        }

        item.NetworkDropId = dropId;
        WorldItemSync.SetDropCount(item, view.Count);
        view.Item = item;
    }

    private static void RemoveDropView(DropView view)
    {
        view.Removed = true;
        if (view.Item == null) return;

        view.Item.ClearWorldState();
        Extensions.Despawn(view.Item.gameObject);
        view.Item = null;
    }

    private void ClearDropViews()
    {
        foreach (DropView view in _dropViews.Values)
            RemoveDropView(view);
        _dropViews.Clear();
    }

    #endregion

    #region 호스트 처리 (방장 데이터에서만 호출)

    /// <summary>자원 데미지를 누적하고, 최대 체력에 도달하면 파괴를 확정한다.</summary>
    public void HostReportDamage(PlayerRef instigator, int instanceId, int amount, int maxHealth, float respawnSeconds)
    {
        if (!HasStateAuthority || instanceId == 0 || amount <= 0) return;

        // 이미 부서진 자원 — 거의 동시에 친 두 번째 타격은 무시 (드롭 중복 방지)
        if (DestroyedResources.ContainsKey(instanceId)) return;

        int total = amount;
        if (ResourceDamages.TryGet(instanceId, out WorldDamageEntry damage))
            total += damage.Damage;

        if (total < maxHealth)
        {
            if (!ResourceDamages.ContainsKey(instanceId) && ResourceDamages.Count >= ResourceDamages.Capacity)
                EvictOldestDamage();

            ResourceDamages.Set(instanceId, new WorldDamageEntry { Damage = total, LastHitTime = Runner.SimulationTime });
            return;
        }

        ResourceDamages.Remove(instanceId);

        if (DestroyedResources.Count >= DestroyedResources.Capacity)
            EvictEarliestRespawn();

        float gameTime = WorldClock.Instance != null ? WorldClock.Instance.TotalInGameSeconds : 0f;
        DestroyedResources.Set(instanceId, new WorldDestroyedEntry
        {
            RespawnTime = gameTime + Mathf.Max(0f, respawnSeconds),
            Breaker = instigator,
        });
    }

    /// <summary>바닥 아이템을 새로 등록한다.</summary>
    public void HostDropItem(string itemKey, int count, Vector3 position)
    {
        if (!HasStateAuthority || string.IsNullOrEmpty(itemKey) || count <= 0) return;

        if (itemKey.Length > 31)
        {
            Debug.LogError($"[NetworkPlayerData] 아이템 키가 너무 길어 드롭을 동기화할 수 없습니다: {itemKey}");
            return;
        }

        if (WorldDrops.Count >= WorldDrops.Capacity)
            EvictOldestDrop();

        int dropId = LastDropId + 1;
        if (dropId <= 0) dropId = 1; // 오버플로 방지 (0은 "네트워크 드롭 아님" 표시)
        LastDropId = dropId;

        WorldDrops.Set(dropId, new WorldDropEntry
        {
            ItemKey = itemKey,
            Count = count,
            Position = position,
        });
    }

    /// <summary>바닥 아이템에서 요청 수량만큼 떼어 요청자에게 지급한다.</summary>
    public void HostPickupItem(PlayerRef requester, int dropId, int amount)
    {
        if (!HasStateAuthority || amount <= 0) return;
        if (!WorldDrops.TryGet(dropId, out WorldDropEntry entry)) return; // 이미 누가 주웠다

        int taken = Mathf.Min(amount, entry.Count);
        if (taken <= 0) return;

        entry.Count -= taken;
        if (entry.Count <= 0)
            WorldDrops.Remove(dropId);
        else
            WorldDrops.Set(dropId, entry);

        NetworkPlayerData requesterData = Main.Network != null ? Main.Network.GetPlayerData(requester) : null;
        if (requesterData == null)
        {
            // 요청자가 그새 나갔다 — 아이템을 되돌려 놓는다
            HostDropItem(entry.ItemKey.ToString(), taken, entry.Position);
            return;
        }

        requesterData.Rpc_GrantPickup(entry.ItemKey.ToString(), taken);
    }

    private void EvictOldestDamage()
    {
        int oldestId = 0;
        float oldestTime = float.MaxValue;
        foreach (KeyValuePair<int, WorldDamageEntry> pair in ResourceDamages)
        {
            if (pair.Value.LastHitTime < oldestTime)
            {
                oldestTime = pair.Value.LastHitTime;
                oldestId = pair.Key;
            }
        }
        ResourceDamages.Remove(oldestId);
    }

    // 파괴 테이블이 가득 차면 가장 먼저 다시 생길 항목을 뺀다
    // (이미 반영한 피어의 청크에는 파괴 표시가 남아 있어 영향은 늦게 들어온 피어에 한정된다)
    private void EvictEarliestRespawn()
    {
        int earliestId = 0;
        float earliestTime = float.MaxValue;
        foreach (KeyValuePair<int, WorldDestroyedEntry> pair in DestroyedResources)
        {
            if (pair.Value.RespawnTime < earliestTime)
            {
                earliestTime = pair.Value.RespawnTime;
                earliestId = pair.Key;
            }
        }
        DestroyedResources.Remove(earliestId);
    }

    // 바닥 아이템이 가득 차면 가장 오래된 것부터 사라진다
    private void EvictOldestDrop()
    {
        int oldestId = int.MaxValue;
        foreach (KeyValuePair<int, WorldDropEntry> pair in WorldDrops)
        {
            if (pair.Key < oldestId) oldestId = pair.Key;
        }
        WorldDrops.Remove(oldestId);
    }

    #endregion

    #region RPC

    /// <summary>클라이언트 → 호스트: 자원(또는 월드 배치 아이템) 데미지 보고.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_ReportResourceDamage(int instanceId, int amount, int maxHealth, float respawnSeconds)
    {
        FindWorldMaster()?.HostReportDamage(OwnerRef, instanceId, amount, maxHealth, respawnSeconds);
    }

    /// <summary>클라이언트 → 호스트: 바닥에 아이템 떨어뜨리기.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestDropItem(string itemKey, int count, Vector3 position)
    {
        FindWorldMaster()?.HostDropItem(itemKey, count, position);
    }

    /// <summary>클라이언트 → 호스트: 바닥 아이템 줍기.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestPickupItem(int dropId, int amount)
    {
        FindWorldMaster()?.HostPickupItem(OwnerRef, dropId, amount);
    }

    /// <summary>호스트 → 요청자: 승인된 수량만큼 인벤토리에 넣는다. (호스트 자신이면 로컬 실행)</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void Rpc_GrantPickup(string itemKey, int amount)
    {
        WorldItemSync.HandlePickupGranted(itemKey, amount);
    }

    private static NetworkPlayerData FindWorldMaster()
    {
        if (Main.Network == null) return null;

        foreach (KeyValuePair<PlayerRef, NetworkPlayerData> pair in Main.Network.GetAllPlayers())
        {
            if (pair.Value != null && pair.Value.IsMaster) return pair.Value;
        }
        return null;
    }

    #endregion
}
#endif

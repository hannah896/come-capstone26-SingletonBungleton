#if PHOTON_FUSION
using System;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerData
{
    // 아이템 요청·지급과 같은 reliable RPC 경로로 선행 작업의 수신 완료를 확인한다.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestSaveBarrier(int request) => Rpc_ConfirmSaveBarrier(request);

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void Rpc_ConfirmSaveBarrier(int request) => NetworkSaveCoordinator.ConfirmItemBarrier(request);

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_ReportSaveLoaded()
    {
        IsLoaded = true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestDropItemState(string itemId, string itemKey, int count, Vector3 position,
        float durability, float spoilRemainingSeconds)
    {
        FindWorldMaster()?.HostDropItem(itemKey, count, position, itemId, durability, spoilRemainingSeconds);
    }

    public NetworkWorldSaveData CaptureNetworkSaveData()
    {
        if (!HasStateAuthority || !IsMaster)
            throw new InvalidOperationException("호스트만 네트워크 월드 상태를 저장할 수 있습니다.");
        var result = new NetworkWorldSaveData { lastDropId = LastDropId };
        foreach (var pair in DestroyedResources)
            result.destroyed.Add(new NetworkDestroyedSaveData { instanceId = pair.Key, respawnTime = pair.Value.RespawnTime });
        foreach (var pair in WorldDrops)
        {
            WorldDropEntry entry = pair.Value;
            // 바닥에 놓인 동안 바뀐 부패 시간과 실제 아이템 상태를 호스트 뷰에서 반영한다.
            ItemStackSaveData state;
            if (_dropViews.TryGetValue(pair.Key, out var view) && view.Item != null)
                state = view.Item.CaptureSaveData();
            else
                throw new InvalidOperationException("바닥 아이템을 준비 중입니다. 잠시 뒤 다시 저장해 주세요.");
            state.count = entry.Count;
            result.drops.Add(new NetworkDropSaveData { dropId = pair.Key, position = entry.Position, item = state });
        }
        return result;
    }

    public void RestoreNetworkSaveData(NetworkWorldSaveData data)
    {
        if (!HasStateAuthority || !IsMaster) throw new InvalidOperationException("호스트만 네트워크 월드 상태를 복원할 수 있습니다.");
        if (data == null || data.destroyed == null || data.drops == null)
            throw new InvalidOperationException("네트워크 월드 저장 데이터가 잘못되었습니다.");
        if (data.destroyed.Count > DestroyedCapacity || data.drops.Count > DropCapacity)
            throw new InvalidOperationException("저장된 월드가 현재 네트워크 상태 용량을 초과합니다.");
        DestroyedResources.Clear();
        ResourceDamages.Clear();
        WorldDrops.Clear();
        ItemKeyNames.Clear();
        LastDropId = Mathf.Max(0, data.lastDropId);
        foreach (var entry in data.destroyed)
        {
            if (entry == null || entry.instanceId == 0 || DestroyedResources.ContainsKey(entry.instanceId))
                throw new InvalidOperationException("파괴된 자원 ID가 잘못되었거나 중복되었습니다.");
            // 과거의 세션 PlayerRef를 저장하지 않는다. 복원은 파괴 보상을 다시 지급하지 않는다.
            DestroyedResources.Set(entry.instanceId, new WorldDestroyedEntry { RespawnTime = entry.respawnTime, Breaker = PlayerRef.None });
        }
        foreach (var entry in data.drops)
        {
            if (entry == null || entry.dropId <= 0 || entry.item == null || entry.item.count <= 0 ||
                string.IsNullOrEmpty(entry.item.itemKey) || entry.item.itemKey.Length > 31 ||
                (entry.item.itemId != null && entry.item.itemId.Length > 63) || WorldDrops.ContainsKey(entry.dropId))
                throw new InvalidOperationException("저장된 바닥 아이템 데이터가 잘못되었습니다.");
            if (!NetworkItemKeys.TryRegister(ItemKeyNames, entry.item.itemKey, IsDropItemHashUsed, out int itemHash))
                throw new InvalidOperationException($"저장된 바닥 아이템 키를 네트워크에 등록할 수 없습니다: {entry.item.itemKey}");
            WorldDrops.Set(entry.dropId, new WorldDropEntry
            {
                ItemHash = itemHash,
                Count = entry.item.count, Position = entry.position,
                Durability = entry.item.durability, SpoilRemainingSeconds = entry.item.spoilRemainingSeconds
            });
            LastDropId = Mathf.Max(LastDropId, entry.dropId);
        }
        _appliedWorld = null;
        _appliedDestroyed.Clear();
        _worldDirty = true;
        _nextPruneTime = 0f;
        ClearDropViews();
    }
}
#endif

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class PlayerSaveAdapter
{
    public static PlayerSaveData Capture(Player player, string playerId, int characterIndex)
    {
        if (player == null || !player.IsSaveReady)
            throw new InvalidOperationException("플레이어 초기화가 아직 완료되지 않았습니다.");
        bool hasLocalStructures = Main.Network == null || !Main.Network.IsInRoom;
        return new PlayerSaveData
        {
            playerId = playerId, characterIndex = characterIndex,
            position = player.transform.position, yaw = player.transform.eulerAngles.y,
            status = player.Stat.CaptureSaveData(), inventory = player.Inventory.CaptureSaveData(),
            hasLocalStructures = hasLocalStructures,
            localStructures = hasLocalStructures ? WorldSaveAdapter.CaptureStructures() : new(),
            crafting = CraftingManager.Instance != null
                ? CraftingManager.Instance.CaptureSaveData() : new CraftingSaveData()
        };
    }

    public static async UniTask WaitUntilReadyAsync(Player player, CancellationToken token = default)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (player != null && !player.IsSaveReady)
        {
            if (Time.realtimeSinceStartup >= deadline)
                throw new TimeoutException("플레이어 초기화가 30초 안에 완료되지 않았습니다.");
            await UniTask.Yield(token);
        }
        token.ThrowIfCancellationRequested();
        if (player == null) throw new OperationCanceledException("플레이어가 초기화 도중 제거되었습니다.");
    }

    public static async UniTask RestoreAsync(Player player, PlayerSaveData data, CancellationToken token = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        await WaitUntilReadyAsync(player, token);
        CraftingManager.Instance?.ValidateSaveData(data.crafting);
        await player.Inventory.RestoreSaveDataAsync(data.inventory, token);
        player.Stat.RestoreSaveData(data.status);
        if (player.Motor != null) player.Motor.Teleport(data.position);
        else player.transform.position = data.position;
        player.transform.rotation = Quaternion.Euler(0f, data.yaw, 0f);
        player.FPCameraController?.RestoreYaw(data.yaw);
        CraftingManager.Instance?.RestoreSaveData(data.crafting);
        player.RefreshStateAfterLoad();
    }
}

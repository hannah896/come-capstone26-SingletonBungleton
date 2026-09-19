using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>빈 플레이 세션에서 실행하는 실제 씬 전환 저장 검증. 기존 저장 경로에는 쓰지 않는다.</summary>
public static class SaveFlowSmokeTest
{
    public static string Result { get; private set; } = "Not started";
    public static string InventoryComparison { get; private set; }

    public static async UniTask RunAsync(string testSavePath, bool continueOnly = false)
    {
        if (!Application.isPlaying || Main.Save.CanSave || Result == "Running")
            throw new InvalidOperationException("진행 중인 게임이 없는 플레이 세션에서 실행해 주세요.");
        var pathProperty = typeof(SaveManager).GetProperty(nameof(SaveManager.SavePath));
        string originalPath = Main.Save.SavePath;
        if (string.IsNullOrEmpty(originalPath) || Path.GetFullPath(testSavePath) == Path.GetFullPath(originalPath))
            throw new InvalidOperationException("초기화 완료 후 별도의 테스트 저장 경로를 지정해 주세요.");
        Result = "Running";
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await UniTask.WaitUntil(() => Main.Scene.IsInitialized && !Main.Scene.IsTransitioning,
                cancellationToken: timeout.Token);
            Player player;
            if (!continueOnly)
            {
                Main.Save.BeginNewGame();
                pathProperty.SetValue(Main.Save, testSavePath);
                WorldGenRequest.Set(WorldBranchSetting.Least, WorldLoopSetting.Never, 456789, WorldSize.Small);
                await Main.Scene.ChangeSceneAsync("GameScene").AttachExternalCancellation(timeout.Token);
                if (!Main.Save.CanSave) throw new InvalidOperationException("새 게임 준비 실패");
                Main.Save.SetCaptureBlocked(true);
                player = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None).First(p => p.IsLocalPlayer);
                ItemDataSO axe = await WorldItemSync.LoadItemDataAsync("Tool_Axe_Stone");
                ItemDataSO berry = await WorldItemSync.LoadItemDataAsync("Resource_Berry");
                ItemDataSO bonfire = await WorldItemSync.LoadItemDataAsync("Tool_Bonfire");
                if (axe == null || berry == null || bonfire == null || bonfire.placementPrefab == null)
                    throw new InvalidOperationException("테스트 아이템 Addressables 누락");
                player.Inventory.AddItem(axe, 1, out _, 27f, -1f);
                player.Inventory.AddItem(berry, 3, out _, -1f, 40f);
                await WorldItemSync.SpawnItemAsync(berry.name, 2, player.transform.position + Vector3.right * 2f,
                    ItemSaveCatalog.Create(berry, 2, spoilRemainingSeconds: 25f));
                var structure = UnityEngine.Object.Instantiate(bonfire.placementPrefab,
                    player.transform.position + Vector3.forward * 3f, Quaternion.identity);
                structure.AddComponent<PersistentStructure>().Initialize(bonfire);
                // 화면 밖 청크까지 동일한 배치 ID로 재생성되는지 실제 월드에서 확인한다.
                var chunk = WorldGenManager.Instance.CurrentLogicData.GetAllChunks().First(c => c.DisposeDatas.Count > 0);
                chunk.MarkObjectDestroyed(chunk.DisposeDatas[0].instanceId, WorldClock.Instance.TotalInGameSeconds + 1440f);

                if (!await Main.Save.SaveAndExitAsync(false)) throw new InvalidOperationException(Main.Save.LastError);
            }
            GameSaveData expected = SaveFileStore.Deserialize(SaveFileStore.ReadText(testSavePath));
            if (!await Main.Save.PrepareLoadAsync(testSavePath, timeout.Token)) throw new InvalidOperationException(Main.Save.LastError);
            await Main.Scene.ChangeSceneAsync("GameScene").AttachExternalCancellation(timeout.Token);
            Main.Save.SetCaptureBlocked(true);
            if (!Main.Save.CanSave) throw new InvalidOperationException("이어하기 준비 실패");
            player = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None).First(p => p.IsLocalPlayer);
            PlayerSaveData actualPlayer = PlayerSaveAdapter.Capture(player, Main.Save.LocalPlayerId, Main.Network.LocalCharacterIndex);
            PlayerSaveData expectedPlayer = expected.players.First(p => p.playerId == Main.Save.LocalPlayerId);
            InventoryComparison = "Expected: " + JsonUtility.ToJson(expectedPlayer.inventory) +
                " Actual: " + JsonUtility.ToJson(actualPlayer.inventory);
            // 로딩 화면 해제 프레임 동안 흐른 시간과 float 반올림만 허용한다.
            foreach (var slot in actualPlayer.inventory.slots)
            {
                var savedSlot = expectedPlayer.inventory.slots.Find(s => s.slotIndex == slot.slotIndex);
                if (savedSlot != null && Mathf.Abs(slot.spoilRemainingSeconds - savedSlot.spoilRemainingSeconds) < 0.1f)
                    slot.spoilRemainingSeconds = savedSlot.spoilRemainingSeconds;
            }
            if (JsonUtility.ToJson(actualPlayer.inventory) != JsonUtility.ToJson(expectedPlayer.inventory))
                throw new InvalidOperationException("인벤토리 슬롯·내구도·부패 시간 복원 불일치: " + InventoryComparison);
            if (Vector3.Distance(actualPlayer.position, expectedPlayer.position) > 0.1f)
                throw new InvalidOperationException("플레이어 위치 복원 불일치");
            WorldSaveData actualWorld = WorldSaveAdapter.Capture();
            if (actualWorld.seed != expected.world.seed || actualWorld.structures.Count != expected.world.structures.Count ||
                actualWorld.droppedItems.Count != expected.world.droppedItems.Count ||
                JsonUtility.ToJson(new DestroyedList { items = actualWorld.destroyedObjects }) !=
                    JsonUtility.ToJson(new DestroyedList { items = expected.world.destroyedObjects }) ||
                Mathf.Abs(actualWorld.totalSeconds - expected.world.totalSeconds) > 0.1f)
                throw new InvalidOperationException("월드 시드·시간·건축물·드롭 복원 불일치");
            Result = "Passed: new game → save and lobby → continue; inventory, durability, spoil time, position, world time, structures, drops";
            Debug.Log("[SaveFlowSmokeTest] " + Result);
        }
        catch (Exception error)
        {
            Result = "Failed: " + error;
            Debug.LogWarning("[SaveFlowSmokeTest] " + Result);
        }
        finally
        {
            Main.Save.EndSession();
            pathProperty.SetValue(Main.Save, originalPath);
        }
    }

    [Serializable] private class DestroyedList { public System.Collections.Generic.List<DestroyedObjectSaveData> items; }
}

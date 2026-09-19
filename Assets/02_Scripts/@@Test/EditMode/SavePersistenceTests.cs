using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading;

public class SavePersistenceTests
{
    private string directory;
    private GameObject inventoryObject;
    private ScriptableObject item;

    [SetUp]
    public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "SingletonSaveTests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (inventoryObject != null) UnityEngine.Object.DestroyImmediate(inventoryObject);
        if (item != null) UnityEngine.Object.DestroyImmediate(item);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void ReplacingSave_RetainsPreviousCompleteFileAsBackup()
    {
        string path = Path.Combine(directory, "slot.json");
        object data = NewGame();
        string first = (string)Static("SaveFileStore", "Serialize", data);
        Static("SaveFileStore", "Write", path, first, false);
        Set(Get(data, "world"), "seed", 987);
        string second = (string)Static("SaveFileStore", "Serialize", data);
        Static("SaveFileStore", "Write", path, second, false);
        Assert.That(File.ReadAllText(path), Is.EqualTo(second));
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }

    [Test]
    public void SavingAfterBackupRecovery_DoesNotOverwriteGoodBackupWithCorruption()
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "slot.json");
        string good = (string)Static("SaveFileStore", "Serialize", NewGame());
        File.WriteAllText(path, "interrupted-write");
        File.WriteAllText(path + ".bak", good);
        Static("SaveFileStore", "Write", path, good, true);
        Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(good));
        Assert.That(Static("SaveFileStore", "Deserialize", File.ReadAllText(path)), Is.Not.Null);
    }

    [Test]
    public void CorruptedPayload_IsRejectedWithoutChangingFiles()
    {
        string json = (string)Static("SaveFileStore", "Serialize", NewGame());
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Deserialize", json.Replace("12345", "98765")));
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Deserialize", "{broken"));
    }

    [Test]
    public void UnsupportedVersion_AndDuplicatePlayerId_AreRejected()
    {
        object data = NewGame();
        Set(data, "generatorVersion", 999);
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Serialize", data));
        Set(data, "generatorVersion", 1);
        var players = (IList)Get(data, "players");
        players.Add(players[0]);
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Serialize", data));
    }

    [Test]
    public void NonFinitePlayerStatus_IsRejectedBeforeWriting()
    {
        object data = NewGame();
        object player = ((IList)Get(data, "players"))[0];
        Set(Get(player, "status"), "hp", float.NaN);
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Serialize", data));
    }

    [Test]
    public void DuplicateInventorySlots_AreRejectedBeforeWriting()
    {
        object inventory = NewInventory("Resource", true);
        Call(inventory, "AddItem", item, 3);
        object save = Call(inventory, "CaptureSaveData");
        var slots = (IList)Get(save, "slots");
        slots.Add(slots[0]);
        object data = NewGame();
        Set(((IList)Get(data, "players"))[0], "inventory", save);
        AssertFailure<InvalidDataException>(() => Static("SaveFileStore", "Serialize", data));
    }

    [Test]
    public void InvalidNetworkDropPosition_IsRejectedBeforeWriting()
    {
        object data = NewGame();
        object network = Activator.CreateInstance(TypeOf("NetworkWorldSaveData"));
        object drop = Activator.CreateInstance(TypeOf("NetworkDropSaveData"));
        Set(drop, "dropId", 1);
        Set(drop, "position", new Vector3(float.PositiveInfinity, 0f, 0f));
        ((IList)Get(network, "drops")).Add(drop);
        Set(Get(data, "world"), "network", network);
        AssertFailure<InvalidOperationException>(() => Static("SaveFileStore", "Serialize", data));
    }

    [Test]
    public void InventoryRestore_PreservesEmptySlots_AndDoesNotMergeSavedStacks()
    {
        object inventory = NewInventory("Resource", true);
        Call(inventory, "AddItem", item, 3);
        object save = Call(inventory, "CaptureSaveData");
        var slots = (IList)Get(save, "slots");
        Set(slots[0], "slotIndex", 2);
        Set(slots[0], "spoilRemainingSeconds", 40f);
        Complete(Call(inventory, "RestoreSaveDataAsync", save, default(System.Threading.CancellationToken)));
        var restored = (IList)Get(Call(inventory, "CaptureSaveData"), "slots");
        Assert.That(restored.Count, Is.EqualTo(1));
        Assert.That(Get(restored[0], "slotIndex"), Is.EqualTo(2));
        Assert.That(Get(restored[0], "count"), Is.EqualTo(3));
        Assert.That((float)Get(restored[0], "spoilRemainingSeconds"), Is.EqualTo(40f).Within(0.1f));
        Assert.That(((IList)inventory.GetType().GetProperty("Slots").GetValue(inventory))[0], Is.Null);
    }

    [Test]
    public void Reequip_AndSaveRestore_PreserveDurabilityWithoutAViewObject()
    {
        object inventory = NewInventory("SurvivalTool", false);
        Set(item, "hasDurability", true);
        Set(item, "maxDurability", 100f);
        object hand = Enum.Parse(TypeOf("EquipSlot"), "Hand");
        Set(item, "equipSlot", hand);
        Call(inventory, "AddItem", item, 1);
        Call(inventory, "SetSlotState", 0, 27f, -1f);
        Assert.That(Call(inventory, "EquipFromSlot", 0), Is.True);
        Assert.That(Call(inventory, "UnequipItem", hand), Is.True);
        Assert.That(Get(Call(inventory, "CaptureSlot", 0), "durability"), Is.EqualTo(27f));
        Assert.That(Call(inventory, "EquipFromSlot", 0), Is.True);
        object save = Call(inventory, "CaptureSaveData");
        Complete(Call(inventory, "RestoreSaveDataAsync", save, default(System.Threading.CancellationToken)));
        object runtime = Call(inventory, "GetEquippedItemInstance", hand);
        Assert.That(runtime.GetType().GetProperty("CurrentDurability").GetValue(runtime), Is.EqualTo(27f));
    }

    [Test]
    public void InvalidInventoryRestore_LeavesExistingItemsUntouched()
    {
        object inventory = NewInventory("Resource", true);
        Call(inventory, "AddItem", item, 3);
        object save = Call(inventory, "CaptureSaveData");
        Set(((IList)Get(save, "slots"))[0], "slotIndex", 99);
        AssertFailure<InvalidOperationException>(() => Complete(Call(inventory, "RestoreSaveDataAsync", save,
            default(System.Threading.CancellationToken))));
        Assert.That(Call(inventory, "GetItemCount", item), Is.EqualTo(3));
    }

    [Test]
    public void Capture_IsIndependentOfLaterInventoryMutations()
    {
        object inventory = NewInventory("Resource", true);
        Call(inventory, "AddItem", item, 3);
        object save = Call(inventory, "CaptureSaveData");
        Call(inventory, "RemoveItem", item, 2);
        Assert.That(Get(((IList)Get(save, "slots"))[0], "count"), Is.EqualTo(3));
    }

    [Test]
    public void DifferentWorlds_KeepSeparateFiles_WhileResavingUpdatesOnlyThatWorld()
    {
        object first = NewGame();
        object second = NewGame();
        string firstPath = (string)Static("SaveCatalog", "GetWorldPath", directory, Get(first, "worldId"));
        string secondPath = (string)Static("SaveCatalog", "GetWorldPath", directory, Get(second, "worldId"));
        string secondJson = (string)Static("SaveFileStore", "Serialize", second);
        Static("SaveFileStore", "Write", firstPath, Static("SaveFileStore", "Serialize", first), false);
        Static("SaveFileStore", "Write", secondPath, secondJson, false);
        Set(Get(first, "world"), "seed", 6789);
        Static("SaveFileStore", "Write", firstPath, Static("SaveFileStore", "Serialize", first), false);
        Assert.That(File.ReadAllText(secondPath), Is.EqualTo(secondJson));
        Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(2));
        Assert.That(Get(Get(Static("SaveFileStore", "Deserialize", File.ReadAllText(firstPath)), "world"), "seed"), Is.EqualTo(6789));
    }

    [Test]
    public void Catalog_DeduplicatesBackup_IncludesLegacyAndOrphanBackup_WithoutSlotLimit()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "slot_0.json"), "legacy");
        File.WriteAllText(Path.Combine(directory, "slot_0.json.bak"), "backup");
        File.WriteAllText(Path.Combine(directory, "orphan.json.bak"), "backup");
        File.WriteAllText(Path.Combine(directory, "ignored.json.tmp"), "partial");
        for (int i = 0; i < 125; i++) File.WriteAllText(Path.Combine(directory, $"world_{i}.json"), "save");
        var paths = (IList)Static("SaveCatalog", "EnumeratePaths", directory);
        Assert.That(paths.Count, Is.EqualTo(127));
        Assert.That(paths.Contains(Path.Combine(directory, "slot_0.json")), Is.True);
        Assert.That(paths.Contains(Path.Combine(directory, "orphan.json")), Is.True);
    }

    [Test]
    public void BeginNewGame_AssignsNewWorldPath_WithoutCreatingOrDeletingFiles()
    {
        object manager = Activator.CreateInstance(TypeOf("SaveManager"));
        manager.GetType().GetProperty("SaveDirectory").SetValue(manager, directory);
        Call(manager, "BeginNewGame");
        string first = (string)manager.GetType().GetProperty("SavePath").GetValue(manager);
        Static("SaveFileStore", "Write", first, Static("SaveFileStore", "Serialize", NewGame()), false);
        Call(manager, "BeginNewGame");
        string second = (string)manager.GetType().GetProperty("SavePath").GetValue(manager);
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(File.Exists(first), Is.True);
        Assert.That(File.Exists(second), Is.False);
    }

    [UnityTest]
    public IEnumerator Catalog_SortsNewestFirst_RecoversBackup_AndIsolatesInvalidFiles()
    {
        object older = NewGame();
        string playerId = (string)Get(((IList)Get(older, "players"))[0], "playerId");
        Set(older, "savedAtUtc", "2026-09-01T00:00:00Z");
        string olderPath = Path.Combine(directory, "slot_0.json");
        Static("SaveFileStore", "Write", olderPath, Static("SaveFileStore", "Serialize", older), false);
        object newer = NewGame();
        Set(((IList)Get(newer, "players"))[0], "playerId", playerId);
        Set(newer, "savedAtUtc", "2026-09-19T00:00:00Z");
        Set(newer, "worldName", "우리 월드");
        string newerPath = (string)Static("SaveCatalog", "GetWorldPath", directory, Get(newer, "worldId"));
        Static("SaveFileStore", "Write", newerPath, Static("SaveFileStore", "Serialize", newer), false);
        File.Copy(newerPath, newerPath + ".bak");
        File.WriteAllText(newerPath, "corrupted");
        File.WriteAllText(Path.Combine(directory, "broken.json"), "corrupted");
        object foreign = NewGame();
        Static("SaveFileStore", "Write", Path.Combine(directory, "foreign.json"), Static("SaveFileStore", "Serialize", foreign), false);
        object awaiter = Call(Static("SaveCatalog", "ListAsync", directory, playerId, CancellationToken.None), "GetAwaiter");
        while (!(bool)awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter)) yield return null;
        var entries = (IList)Call(awaiter, "GetResult");
        Assert.That(entries.Count, Is.EqualTo(4));
        Assert.That(Get(entries[0], "Path"), Is.EqualTo(newerPath));
        Assert.That(Get(entries[0], "RecoveredBackup"), Is.True);
        Assert.That(Get(entries[0], "Name"), Does.StartWith("우리 월드"));
        Assert.That(Get(entries[1], "Path"), Is.EqualTo(olderPath));
        Assert.That(entries.Cast<object>().Count(e => Get(e, "Error") != null), Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator Catalog_CancelledRead_DoesNotReturnSave()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        object awaiter = Call(Static("SaveCatalog", "ReadAsync", Path.Combine(directory, "missing.json"), cancellation.Token), "GetAwaiter");
        while (!(bool)awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter)) yield return null;
        AssertFailure<OperationCanceledException>(() => Call(awaiter, "GetResult"));
    }

    private object NewInventory(string itemType, bool stackable)
    {
        inventoryObject = new GameObject("SaveInventoryTest");
        object inventory = inventoryObject.AddComponent(TypeOf("PlayerInventory"));
        Call(inventory, "SetSlotCount", 4);
        item = ScriptableObject.CreateInstance(TypeOf("ItemDataSO"));
        item.name = "save-test-" + Guid.NewGuid().ToString("N");
        Set(item, "itemID", item.name);
        Set(item, "itemType", Enum.Parse(TypeOf("ItemType"), itemType));
        Set(item, "isStackable", stackable);
        Set(item, "maxStack", stackable ? 10 : 1);
        return inventory;
    }

    private static object NewGame()
    {
        object game = Activator.CreateInstance(TypeOf("GameSaveData"));
        Set(game, "worldId", Guid.NewGuid().ToString("N"));
        object world = Activator.CreateInstance(TypeOf("WorldSaveData"));
        Set(world, "seed", 12345);
        Set(game, "world", world);
        object player = Activator.CreateInstance(TypeOf("PlayerSaveData"));
        Set(player, "playerId", Guid.NewGuid().ToString("N"));
        ((IList)Get(game, "players")).Add(player);
        return game;
    }

    private static void Complete(object task)
    {
        object awaiter = Call(task, "GetAwaiter");
        Assert.That(awaiter.GetType().GetProperty("IsCompleted").GetValue(awaiter), Is.True,
            "테스트 아이템은 카탈로그 캐시에서 동기적으로 복원되어야 합니다.");
        Call(awaiter, "GetResult");
    }
    private static void AssertFailure<T>(TestDelegate action) where T : Exception
    {
        var error = Assert.Throws<TargetInvocationException>(action);
        Exception cause = error.InnerException;
        while (cause is TargetInvocationException nested) cause = nested.InnerException;
        Assert.That(cause, Is.InstanceOf<T>());
    }
    private static object Static(string type, string method, params object[] args) => Invoke(TypeOf(type), null, method, args);
    private static object Call(object target, string method, params object[] args) => Invoke(target.GetType(), target, method, args);
    private static object Invoke(Type type, object target, string method, object[] args) => type.GetMethods()
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
}

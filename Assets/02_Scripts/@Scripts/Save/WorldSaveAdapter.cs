using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>월드 런타임을 파일용 데이터로 변환한다. 파일 입출력은 SaveManager가 담당한다.</summary>
public static class WorldSaveAdapter
{
    public static WorldSaveData Capture()
    {
        WorldGenManager world = WorldGenManager.Instance;
        if (world == null || world.CurrentLogicData == null || world.WorldSettings == null)
            throw new InvalidOperationException("월드 생성이 끝난 뒤에 저장할 수 있습니다.");

        var settings = world.WorldSettings;
        var data = new WorldSaveData
        {
            seed = settings.WorldSeed,
            branch = settings.WorldBranch,
            loop = settings.WorldLoop,
            size = settings.WorldSize,
            totalSeconds = WorldClock.Instance != null ? WorldClock.Instance.SaveTime() : 0f
        };

        foreach (ChunkData chunk in world.CurrentLogicData.GetAllChunks())
            foreach (var destroyed in chunk.DestroyedObjects)
                data.destroyedObjects.Add(new DestroyedObjectSaveData
                {
                    instanceId = destroyed.Key,
                    respawnTime = destroyed.Value
                });
        data.destroyedObjects.Sort((a, b) => a.instanceId.CompareTo(b.instanceId));

        data.structures = CaptureStructures();

        if (Main.Network == null || !Main.Network.IsInRoom)
        {
            foreach (PersistentDroppedItem entry in PersistentDroppedItem.All.ToArray())
            {
                if (entry == null || entry.Item == null || !entry.Item.isActiveAndEnabled ||
                    entry.Item.IsWorldPlaced || entry.Item.NetworkDropId != 0) continue;
                data.droppedItems.Add(new DroppedItemSaveData
                {
                    id = entry.Id,
                    item = entry.Item.CaptureSaveData(),
                    position = entry.Item.transform.position
                });
            }
            data.droppedItems.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        }

        NetworkSaveCoordinator.CaptureWorldState(data);
        Validate(data);
        return data;
    }

    public static List<StructureSaveData> CaptureStructures()
    {
        var structures = new List<StructureSaveData>();
        foreach (PersistentStructure entry in PersistentStructure.All.ToArray())
        {
            if (entry == null || entry.SourceItem == null) continue;
            var structure = new StructureSaveData
            {
                id = entry.Id,
                sourceItem = ItemSaveCatalog.Create(entry.SourceItem, 1),
                position = entry.transform.position,
                rotation = entry.transform.rotation,
                scale = entry.transform.localScale
            };
            StorageStation storage = entry.GetComponentInChildren<StorageStation>(true);
            if (storage != null)
            {
                structure.slotCount = storage.SlotCount;
                structure.slots = storage.CaptureSlots();
            }
            CookingPot cookingPot = entry.GetComponentInChildren<CookingPot>(true);
            structure.isCooking = cookingPot != null && cookingPot.IsCooking;
            structures.Add(structure);
        }
        structures.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return structures;
    }

    /// <summary>어떤 청크도 화면에 스폰되기 전에 실행한다. 드롭/채집 효과는 발생시키지 않는다.</summary>
    public static void ApplyChunkChanges(WorldSaveData data, WorldLogicData logic)
    {
        Validate(data);
        if (logic == null) throw new ArgumentNullException(nameof(logic));
        foreach (ChunkData chunk in logic.GetAllChunks()) chunk.DestroyedObjects.Clear();
        foreach (DestroyedObjectSaveData destroyed in data.destroyedObjects)
        {
            ChunkData chunk = logic.FindChunkByInstanceId(destroyed.instanceId);
            if (chunk == null)
                throw new InvalidOperationException($"저장한 월드 배치 ID를 찾을 수 없습니다: {destroyed.instanceId}. 월드 생성 버전을 확인하세요.");
            // 저장 시각에 이미 재생 시간이 지난 자원은 바로 생성한다.
            if (destroyed.respawnTime > data.totalSeconds)
                chunk.MarkObjectDestroyed(destroyed.instanceId, destroyed.respawnTime);
        }
    }

    /// <summary>지형 준비 후 건축물과 싱글플레이 드롭을 한 번만 복원한다.</summary>
    public static async UniTask RestoreRuntimeAsync(WorldSaveData data, CancellationToken token = default)
    {
        Validate(data);
        // 멀티 구조물과 드롭은 호스트 네트워크 테이블을 통해 전 피어에 복제된다.
        if (Main.Network != null && Main.Network.IsInRoom)
            return;

        List<StructureSaveData> structures = data.structures;
        ValidateStructures(structures);
        var structureItems = new List<ItemDataSO>(structures.Count);
        foreach (StructureSaveData saved in structures)
        {
            token.ThrowIfCancellationRequested();
            ItemDataSO source = await ItemSaveCatalog.ResolveAsync(saved.sourceItem, token);
            if (source == null || source.placementPrefab == null)
                throw new InvalidOperationException($"저장한 건축물 프리팹이 없습니다: {saved.sourceItem.itemKey}");
            structureItems.Add(source);
        }

        foreach (PersistentStructure old in PersistentStructure.All.ToArray())
        {
            if (old == null) continue;
            old.Unregister();
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
        for (int index = 0; index < structures.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            StructureSaveData saved = structures[index];
            ItemDataSO source = structureItems[index];
            GameObject instance = UnityEngine.Object.Instantiate(source.placementPrefab, saved.position, saved.rotation);
            instance.transform.localScale = saved.scale;
            Extensions.GetOrAddComponent<PersistentStructure>(instance).Initialize(source, saved.id);
            StorageStation storage = instance.GetComponentInChildren<StorageStation>(true);
            if (saved.slotCount > 0)
            {
                if (storage == null) throw new InvalidOperationException("저장한 보관함의 컴포넌트가 없습니다.");
                await storage.RestoreSlotsAsync(saved.slotCount, saved.slots, token);
            }
            CookingPot pot = instance.GetComponentInChildren<CookingPot>(true);
            if (pot != null && saved.isCooking) pot.TryStartCooking();
        }

        foreach (PersistentDroppedItem old in PersistentDroppedItem.All.ToArray())
        {
            if (old == null || old.Item == null) continue;
            old.Unregister();
            Extensions.Despawn(old.Item.gameObject);
        }
        foreach (DroppedItemSaveData saved in data.droppedItems)
        {
            token.ThrowIfCancellationRequested();
            ItemDataSO source = await ItemSaveCatalog.ResolveAsync(saved.item, token);
            Item item = await WorldItemSync.SpawnItemAsync(source.name, saved.item.count, saved.position, saved.item);
            token.ThrowIfCancellationRequested();
            if (item == null) throw new InvalidOperationException($"드롭 복원 실패: {saved.item.itemKey}");
            Extensions.GetOrAddComponent<PersistentDroppedItem>(item.gameObject).Initialize(item, saved.id);
        }
    }

    public static void Validate(WorldSaveData data)
    {
        if (data == null) throw new InvalidOperationException("월드 저장 데이터가 없습니다.");
        if (!IsFinite(data.totalSeconds) || data.totalSeconds < 0f ||
            !Enum.IsDefined(typeof(WorldBranchSetting), data.branch) ||
            !Enum.IsDefined(typeof(WorldLoopSetting), data.loop) || !Enum.IsDefined(typeof(WorldSize), data.size))
            throw new InvalidOperationException("월드 생성 설정 또는 저장 시간이 올바르지 않습니다.");
        if (data.destroyedObjects == null || data.structures == null || data.droppedItems == null)
            throw new InvalidOperationException("월드 저장 목록이 누락되었습니다.");

        var placementIds = new HashSet<int>();
        foreach (var destroyed in data.destroyedObjects)
            if (destroyed == null || destroyed.instanceId == 0 || !placementIds.Add(destroyed.instanceId) ||
                !IsFinite(destroyed.respawnTime) || destroyed.respawnTime < 0f)
                throw new InvalidOperationException("월드 채집 기록이 올바르지 않습니다.");
        ValidateStructures(data.structures);
        var ids = new HashSet<string>();
        foreach (var drop in data.droppedItems)
        {
            if (drop == null || string.IsNullOrWhiteSpace(drop.id) || !ids.Add(drop.id) || !IsFinite(drop.position))
                throw new InvalidOperationException("바닥 아이템 저장 데이터가 올바르지 않습니다.");
            ValidateItem(drop.item);
        }
        if (data.network != null)
        {
            var network = data.network;
            if (network.destroyed == null || network.drops == null || network.lastDropId < 0 ||
                network.destroyed.Count > 256 || network.drops.Count > 256)
                throw new InvalidOperationException("네트워크 월드 저장 목록이 올바르지 않습니다.");
            var networkIds = new HashSet<int>();
            foreach (var entry in network.destroyed)
                if (entry == null || entry.instanceId == 0 || !networkIds.Add(entry.instanceId) ||
                    !IsFinite(entry.respawnTime) || entry.respawnTime < 0f)
                    throw new InvalidOperationException("네트워크 채집 기록이 올바르지 않습니다.");
            networkIds.Clear();
            foreach (var drop in network.drops)
            {
                if (drop == null || drop.dropId <= 0 || !networkIds.Add(drop.dropId) || !IsFinite(drop.position))
                    throw new InvalidOperationException("네트워크 드롭 정보가 올바르지 않습니다.");
                ValidateItem(drop.item);
                if (drop.item.itemKey.Length > 31 || drop.item.itemId.Length > 63)
                    throw new InvalidOperationException("네트워크 아이템 ID 길이가 허용 범위를 넘었습니다.");
            }
        }
    }

    public static void ValidateStructures(List<StructureSaveData> structures)
    {
        if (structures == null) throw new InvalidOperationException("건축물 저장 목록이 없습니다.");
        var ids = new HashSet<string>();
        foreach (var structure in structures)
        {
            if (structure == null || string.IsNullOrWhiteSpace(structure.id) || !ids.Add(structure.id) ||
                !IsFinite(structure.position) || !IsFinite(structure.scale) ||
                structure.scale.x <= 0f || structure.scale.y <= 0f || structure.scale.z <= 0f ||
                !IsFinite(structure.rotation) || structure.slotCount < 0 || structure.slotCount > 256 || structure.slots == null)
                throw new InvalidOperationException("건축물 저장 데이터가 올바르지 않습니다.");
            ValidateItem(structure.sourceItem);
            var slotIds = new HashSet<int>();
            foreach (var slot in structure.slots)
            {
                ValidateItem(slot);
                if (slot.slotIndex < 0 || slot.slotIndex >= structure.slotCount || !slotIds.Add(slot.slotIndex))
                    throw new InvalidOperationException("보관함 슬롯 정보가 올바르지 않습니다.");
            }
        }
    }

    public static void ValidateItem(ItemStackSaveData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.itemId) || string.IsNullOrWhiteSpace(item.itemKey) || item.count <= 0 ||
            !IsFinite(item.durability) || item.durability < -1f ||
            !IsFinite(item.spoilRemainingSeconds) || item.spoilRemainingSeconds < -1f)
            throw new InvalidOperationException("아이템 저장 데이터가 올바르지 않습니다.");
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) &&
        IsFinite(value.w) && (value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w) > 0.0001f;
}

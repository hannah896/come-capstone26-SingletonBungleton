#if PHOTON_FUSION
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>설치된 구조물 1개의 복제 단위 (키 = 호스트가 발급한 구조물 ID).</summary>
public struct StructureNetState : INetworkStruct
{
    // 설치한 아이템 키(= ItemDataSO 이름)의 해시. 실제 키는 ItemKeyNames에서 찾는다. 모든 피어가 이 SO의 placementPrefab을 설치한다.
    public int ItemHash;
    public Vector3 Position;
    public Quaternion Rotation;
    // Vector3.zero는 일반 설치(프리팹 기본 스케일), 양수 값은 저장에서 복원한 스케일이다.
    public Vector3 Scale;

    // 요리솥 전용: 요리 중인지
    public NetworkBool IsCooking;
}

/// <summary>보관함 슬롯 1칸 (키 = 구조물 ID * SlotKeyScale + 슬롯 인덱스). 빈 슬롯은 테이블에 넣지 않는다.</summary>
public struct StorageSlotNetState : INetworkStruct
{
    public int ItemHash;
    public int Count;
}

/// <summary>
/// 구조물 복제 디렉터. 세션에 단 하나만 존재하며 호스트가 스폰한다.
///
/// 구조물 프리팹에는 NetworkObject를 붙이지 않는다. 대신 이 디렉터가 "무엇이 어디에 설치됐는지"와
/// 보관함 슬롯 내용을 테이블로 복제하고, 모든 피어(호스트 포함)가 테이블을 보고 자기 씬에 구조물을 만들거나 지운다.
///
/// - 설치/철거/줍기/보관함 조작은 전부 호스트에 요청 → 호스트가 테이블을 갱신
/// - 보관함 내용: 호스트의 실제 StorageStation이 원본이고, 변경될 때마다 슬롯 테이블에 스냅샷을 기록한다.
///   클라는 테이블을 받아 자기 StorageStation에 덮어쓴다.
/// - 인벤토리에서 빠지는 아이템은 요청자가 먼저 빼고, 호스트가 못 넣은 만큼 Rpc_GrantPickup으로 돌려준다.
///
/// 게임 코드는 이 클래스를 직접 부르지 않고 <see cref="StructureSync"/>를 통해 싱글/멀티 공통으로 사용한다.
///
/// 상태 크기: Fusion 오브젝트 상한(32KB) 때문에 테이블에는 아이템 키 해시만 넣고, 키 문자열은 ItemKeyNames에 한 번만 둔다.
/// </summary>
public class NetworkStructureDirector : NetworkBehaviour
{
    public const int StructureCapacity = 128;
    public const int SlotCapacity = 256;
    public const int ItemNameCapacity = 48;

    // 슬롯 키 = 구조물 ID * SlotKeyScale + 슬롯 인덱스 (보관함 슬롯 수는 이 값 미만이어야 한다)
    private const int SlotKeyScale = 64;

    [Networked, Capacity(StructureCapacity)]
    private NetworkDictionary<int, StructureNetState> Structures => default;

    [Networked, Capacity(SlotCapacity)]
    private NetworkDictionary<int, StorageSlotNetState> StorageSlots => default;

    // 해시 → 아이템 키 이름표 (구조물·보관함 테이블이 참조)
    [Networked, Capacity(ItemNameCapacity)]
    private NetworkDictionary<int, NetworkString<_32>> ItemKeyNames => default;

    [Networked] private int LastStructureId { get; set; }

    /// <summary>현재 세션의 디렉터.</summary>
    public static NetworkStructureDirector Instance { get; private set; }

    /// <summary>세션이 살아 있어 요청이 가능한지.</summary>
    public static bool IsActive => Instance != null && Instance.Object != null && Instance.Object.IsValid;

    // 구조물 ID → 이 피어 씬에 만든 오브젝트
    private readonly Dictionary<int, View> _views = new();

    // 아이템 키 → SO 캐시 (보관함 반영은 동기로 해야 해서 미리 로드해 둔다)
    private readonly Dictionary<string, ItemDataSO> _itemCache = new();
    private readonly HashSet<string> _loadingKeys = new();
    private readonly HashSet<string> _failedKeys = new(); // 로드 실패한 키 — 매 프레임 재시도하지 않는다
    // 불러오기 직후 호스트 뷰가 생성되면 실제 보관함에 적용할 저장 상태
    private readonly Dictionary<int, StructureSaveData> _pendingRestores = new();

    private readonly HashSet<int> _seenIds = new();
    private readonly List<int> _removeBuffer = new();

    private ChangeDetector _changes;
    private bool _dirty;

    private sealed class View
    {
        public GameObject Root;
        public bool Loading;
        public bool Failed;   // 프리팹을 찾지 못함 — 재시도하지 않는다
        public bool Removed;
        public StorageStation Storage;
        public CookingPot Pot;
        public System.Action StorageHandler;
        public System.Action<bool> CookingHandler;
    }

    #region Lifecycle

    public override void Spawned()
    {
        Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _dirty = true;
        if (HasStateAuthority)
            NetworkSaveCoordinator.RestoreHostStructures();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;

        foreach (View view in _views.Values)
            DestroyView(view);
        _views.Clear();
    }

    // 모든 피어(호스트 포함): 복제된 테이블과 자기 씬의 차이를 반영한다
    public override void Render()
    {
        foreach (string _ in _changes.DetectChanges(this))
            _dirty = true;

        // 월드 생성 중이면 기다렸다가 준비되는 순간 한꺼번에 반영한다
        if (!IsWorldReady()) return;

        // 씬 리로드 등으로 오브젝트가 파괴됐으면 다시 만든다
        foreach (View view in _views.Values)
        {
            if (!view.Loading && !view.Failed && view.Root == null)
            {
                _dirty = true;
                break;
            }
        }

        if (!_dirty) return;
        _dirty = false;

        SyncStructures();

        // 호스트의 보관함은 원본이라 테이블로 덮어쓰지 않는다
        if (!HasStateAuthority)
            SyncStorages();
    }

    private static bool IsWorldReady()
    {
        WorldGenManager manager = WorldGenManager.Instance;
        return manager == null || manager.CurrentLogicData != null; // WorldGen이 없는 테스트 씬은 바로 반영
    }

    #endregion

    #region 반영 (모든 피어)

    private void SyncStructures()
    {
        _seenIds.Clear();
        foreach (KeyValuePair<int, StructureNetState> pair in Structures)
        {
            _seenIds.Add(pair.Key);

            if (_views.TryGetValue(pair.Key, out View view))
            {
                bool destroyed = !view.Loading && !view.Failed && view.Root == null;
                if (!destroyed)
                {
                    // 요리 상태 (호스트는 원본이 이미 그 상태다)
                    if (!HasStateAuthority && view.Pot != null && view.Pot.IsCooking != pair.Value.IsCooking)
                        view.Pot.ApplyNetworkCooking(pair.Value.IsCooking);
                    continue;
                }

                DestroyView(view);
            }

            view = new View { Loading = true };
            _views[pair.Key] = view;
            CreateViewAsync(pair.Key, pair.Value, view).Forget();
        }

        // 철거/회수돼 테이블에서 빠진 구조물
        _removeBuffer.Clear();
        foreach (int id in _views.Keys)
        {
            if (!_seenIds.Contains(id)) _removeBuffer.Add(id);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            DestroyView(_views[_removeBuffer[i]]);
            _views.Remove(_removeBuffer[i]);
        }
    }

    private async UniTaskVoid CreateViewAsync(int id, StructureNetState state, View view)
    {
        string key = NetworkItemKeys.Resolve(ItemKeyNames, state.ItemHash);
        ItemDataSO itemData = await GetItemDataAsync(key);

        if (view.Removed) return; // 로드하는 사이 철거됐다

        view.Loading = false;
        if (itemData == null || itemData.placementPrefab == null)
        {
            view.Failed = true;
            Debug.LogError($"[StructureDirector] '{key}'의 placementPrefab을 찾지 못해 구조물을 설치하지 못했습니다.");
            return;
        }

        GameObject root = Instantiate(itemData.placementPrefab, state.Position, state.Rotation);
        if (state.Scale.x > 0f && state.Scale.y > 0f && state.Scale.z > 0f)
            root.transform.localScale = state.Scale;
        PlacedStructure placed = Extensions.GetOrAddComponent<PlacedStructure>(root);
        placed.NetworkId = id;
        placed.ItemKey = key;

        view.Root = root;
        view.Storage = root.GetComponentInChildren<StorageStation>(true);
        view.Pot = view.Storage as CookingPot;

        if (HasStateAuthority)
        {
            if (_pendingRestores.TryGetValue(id, out StructureSaveData saved))
            {
                root.transform.localScale = saved.scale;
                if (saved.slotCount > 0)
                {
                    if (view.Storage == null)
                        throw new System.InvalidOperationException("저장된 보관함 구조물에 StorageStation이 없습니다.");
                    await view.Storage.RestoreSlotsAsync(saved.slotCount, saved.slots);
                }
                if (saved.isCooking && view.Pot != null)
                    view.Pot.TryStartCooking();
                _pendingRestores.Remove(id);
            }

            // 호스트: 실제 보관함/요리솥이 원본 — 바뀔 때마다 테이블에 기록한다
            if (view.Storage != null)
            {
                StorageStation storage = view.Storage;
                view.StorageHandler = () => WriteStorageSnapshot(id, storage);
                storage.OnStorageChanged += view.StorageHandler;
                WriteStorageSnapshot(id, storage);
            }

            if (view.Pot != null)
            {
                view.CookingHandler = cooking => WriteCooking(id, cooking);
                view.Pot.OnCookingStateChanged += view.CookingHandler;
            }
        }
        else
        {
            // 클라: 방금 만든 구조물에 보관함/요리 상태를 반영한다
            if (view.Pot != null) view.Pot.ApplyNetworkCooking(state.IsCooking);
            _dirty = true;
        }
    }

    private void DestroyView(View view)
    {
        view.Removed = true;

        if (view.Storage != null && view.StorageHandler != null)
            view.Storage.OnStorageChanged -= view.StorageHandler;
        if (view.Pot != null && view.CookingHandler != null)
            view.Pot.OnCookingStateChanged -= view.CookingHandler;

        if (view.Root != null)
            Destroy(view.Root);

        view.Root = null;
    }

    // 클라: 슬롯 테이블을 각 보관함에 덮어쓴다
    private void SyncStorages()
    {
        foreach (KeyValuePair<int, View> pair in _views)
        {
            StorageStation storage = pair.Value.Storage;
            if (storage == null || pair.Value.Root == null) continue;

            int slotCount = Mathf.Min(storage.SlotCount, SlotKeyScale);
            var items = new ItemDataSO[slotCount];
            var counts = new int[slotCount];
            bool pending = false;

            for (int i = 0; i < slotCount; i++)
            {
                if (!StorageSlots.TryGet(SlotKey(pair.Key, i), out StorageSlotNetState slot) || slot.Count <= 0)
                    continue;

                string itemKey = NetworkItemKeys.Resolve(ItemKeyNames, slot.ItemHash);
                if (itemKey == null)
                {
                    pending = true; // 이름표가 아직 반영되지 않았다
                    continue;
                }
                if (_failedKeys.Contains(itemKey)) continue;

                if (!TryGetCachedItem(itemKey, out ItemDataSO itemData))
                {
                    pending = true; // 로드가 끝나면 _dirty가 켜져 다시 반영된다
                    continue;
                }

                items[i] = itemData;
                counts[i] = slot.Count;
            }

            if (!pending)
                storage.ApplyNetworkSlots(items, counts);
        }
    }

    #endregion

    #region 호스트 기록

    private void WriteStorageSnapshot(int id, StorageStation storage)
    {
        if (!HasStateAuthority || storage == null) return;

        int slotCount = Mathf.Min(storage.SlotCount, SlotKeyScale);
        for (int i = 0; i < slotCount; i++)
        {
            int key = SlotKey(id, i);
            ItemDataSO itemData = storage.Slots[i];
            int count = storage.StackCounts[i];

            if (itemData == null || count <= 0)
            {
                StorageSlots.Remove(key);
                continue;
            }

            _itemCache[itemData.name] = itemData;

            if (!StorageSlots.ContainsKey(key) && StorageSlots.Count >= StorageSlots.Capacity)
            {
                Debug.LogWarning($"[StructureDirector] 보관함 슬롯 테이블이 가득 찼습니다(Capacity={SlotCapacity}). 일부 슬롯이 동기화되지 않습니다.");
                continue;
            }

            if (!NetworkItemKeys.TryRegister(ItemKeyNames, itemData.name, IsItemHashUsed, out int itemHash))
                continue;

            StorageSlots.Set(key, new StorageSlotNetState { ItemHash = itemHash, Count = count });
        }
    }

    private void WriteCooking(int id, bool cooking)
    {
        if (!HasStateAuthority || !Structures.TryGet(id, out StructureNetState state)) return;

        state.IsCooking = cooking;
        Structures.Set(id, state);
    }

    private void RemoveStructureEntry(int id)
    {
        Structures.Remove(id);
        for (int i = 0; i < SlotKeyScale; i++)
            StorageSlots.Remove(SlotKey(id, i));
    }

    private bool HasStoredItems(int id)
    {
        for (int i = 0; i < SlotKeyScale; i++)
        {
            if (StorageSlots.ContainsKey(SlotKey(id, i))) return true;
        }
        return false;
    }

    private static int SlotKey(int id, int slotIndex) => id * SlotKeyScale + slotIndex;

    // 구조물/보관함 테이블 중 이 아이템 해시를 쓰는 항목이 있는지 (이름표 정리용)
    private bool IsItemHashUsed(int itemHash)
    {
        foreach (KeyValuePair<int, StructureNetState> pair in Structures)
        {
            if (pair.Value.ItemHash == itemHash) return true;
        }
        foreach (KeyValuePair<int, StorageSlotNetState> pair in StorageSlots)
        {
            if (pair.Value.ItemHash == itemHash) return true;
        }
        return false;
    }

    #endregion

    #region 저장 / 불러오기

    public List<StructureSaveData> CaptureSaveData()
    {
        if (!HasStateAuthority)
            throw new System.InvalidOperationException("호스트만 공유 건축물 상태를 저장할 수 있습니다.");

        var result = new List<StructureSaveData>(Structures.Count);
        foreach (KeyValuePair<int, StructureNetState> pair in Structures)
        {
            string itemKey = NetworkItemKeys.Resolve(ItemKeyNames, pair.Value.ItemHash);
            if (string.IsNullOrEmpty(itemKey) || !_itemCache.TryGetValue(itemKey, out ItemDataSO itemData) || itemData == null ||
                !_views.TryGetValue(pair.Key, out View view) || view.Root == null || view.Loading || view.Failed)
                throw new System.InvalidOperationException("공유 건축물을 준비 중입니다. 잠시 뒤 다시 저장해 주세요.");

            StorageStation storage = view.Storage;
            result.Add(new StructureSaveData
            {
                id = pair.Key.ToString(),
                sourceItem = ItemSaveCatalog.Create(itemData, 1),
                position = pair.Value.Position,
                rotation = pair.Value.Rotation,
                scale = view.Root.transform.localScale,
                slotCount = storage != null ? storage.SlotCount : 0,
                slots = storage != null ? storage.CaptureSlots() : new List<ItemStackSaveData>(),
                isCooking = view.Pot != null && view.Pot.IsCooking
            });
        }
        result.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
        return result;
    }

    public void RestoreSaveData(List<StructureSaveData> savedStructures)
    {
        if (!HasStateAuthority)
            throw new System.InvalidOperationException("호스트만 공유 건축물 상태를 복원할 수 있습니다.");
        if (savedStructures == null || savedStructures.Count > StructureCapacity)
            throw new System.InvalidOperationException("저장된 공유 건축물 수가 네트워크 용량을 초과합니다.");

        int slotTotal = 0;
        var names = new Dictionary<int, string>();
        foreach (StructureSaveData saved in savedStructures)
        {
            if (saved == null || saved.sourceItem == null || saved.slots == null || saved.slotCount > SlotKeyScale)
                throw new System.InvalidOperationException("저장된 공유 건축물 데이터가 올바르지 않습니다.");
            RegisterSavedKey(saved.sourceItem.itemKey, names);
            slotTotal += saved.slots.Count;
            foreach (ItemStackSaveData slot in saved.slots)
                RegisterSavedKey(slot.itemKey, names);
        }
        if (slotTotal > SlotCapacity || names.Count > ItemNameCapacity)
            throw new System.InvalidOperationException("저장된 공유 건축물 내용이 네트워크 용량을 초과합니다.");

        foreach (View view in _views.Values) DestroyView(view);
        _views.Clear();
        _pendingRestores.Clear();
        Structures.Clear();
        StorageSlots.Clear();
        ItemKeyNames.Clear();
        LastStructureId = 0;

        for (int index = 0; index < savedStructures.Count; index++)
        {
            StructureSaveData saved = savedStructures[index];
            int id = index + 1;
            if (!NetworkItemKeys.TryRegister(ItemKeyNames, saved.sourceItem.itemKey, IsItemHashUsed, out int structureHash))
                throw new System.InvalidOperationException($"건축물 키를 네트워크에 등록할 수 없습니다: {saved.sourceItem.itemKey}");

            Structures.Set(id, new StructureNetState
            {
                ItemHash = structureHash,
                Position = saved.position,
                Rotation = saved.rotation,
                Scale = saved.scale,
                IsCooking = saved.isCooking
            });
            foreach (ItemStackSaveData slot in saved.slots)
            {
                if (!NetworkItemKeys.TryRegister(ItemKeyNames, slot.itemKey, IsItemHashUsed, out int itemHash))
                    throw new System.InvalidOperationException($"보관함 아이템 키를 네트워크에 등록할 수 없습니다: {slot.itemKey}");
                StorageSlots.Set(SlotKey(id, slot.slotIndex), new StorageSlotNetState
                {
                    ItemHash = itemHash,
                    Count = slot.count
                });
            }
            _pendingRestores[id] = saved;
            LastStructureId = id;
        }
        _dirty = true;
    }

    private static void RegisterSavedKey(string itemKey, Dictionary<int, string> names)
    {
        if (string.IsNullOrEmpty(itemKey))
            throw new System.InvalidOperationException("저장된 네트워크 아이템 키가 비어 있습니다.");
        int hash = NetworkItemKeys.Hash(itemKey);
        if (hash == 0 || itemKey.Length > NetworkItemKeys.MaxKeyLength ||
            (names.TryGetValue(hash, out string existing) && existing != itemKey))
            throw new System.InvalidOperationException($"저장된 네트워크 아이템 키가 올바르지 않습니다: {itemKey}");
        names[hash] = itemKey;
    }

    #endregion

    #region 요청 (StructureSync에서 호출)

    public void RequestPlace(string itemKey, Vector3 position, Quaternion rotation)
    {
        if (HasStateAuthority) HostPlace(Runner.LocalPlayer, itemKey, position, rotation);
        else Rpc_RequestPlace(itemKey, position, rotation);
    }

    public void RequestDemolish(int id)
    {
        if (HasStateAuthority) HostRemove(Runner.LocalPlayer, id, giveBack: false);
        else Rpc_RequestRemove(id, false);
    }

    public void RequestPickup(int id)
    {
        if (HasStateAuthority) HostRemove(Runner.LocalPlayer, id, giveBack: true);
        else Rpc_RequestRemove(id, true);
    }

    public void RequestDeposit(int id, int slotIndex, string itemKey, int amount)
    {
        if (HasStateAuthority) HostDepositAsync(Runner.LocalPlayer, id, slotIndex, itemKey, amount).Forget();
        else Rpc_RequestDeposit(id, slotIndex, itemKey, amount);
    }

    public void RequestWithdraw(int id, int slotIndex, int amount)
    {
        if (HasStateAuthority) HostWithdraw(Runner.LocalPlayer, id, slotIndex, amount);
        else Rpc_RequestWithdraw(id, slotIndex, amount);
    }

    public void RequestSwapSlots(int id, int fromIndex, int toIndex)
    {
        if (HasStateAuthority) HostSwapSlots(id, fromIndex, toIndex);
        else Rpc_RequestSwapSlots(id, fromIndex, toIndex);
    }

    public void RequestCooking(int id, bool start)
    {
        if (HasStateAuthority) HostSetCooking(id, start);
        else Rpc_RequestCooking(id, start);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestPlace(string itemKey, Vector3 position, Quaternion rotation, RpcInfo info = default)
        => HostPlace(info.Source, itemKey, position, rotation);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestRemove(int id, NetworkBool giveBack, RpcInfo info = default)
        => HostRemove(info.Source, id, giveBack);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestDeposit(int id, int slotIndex, string itemKey, int amount, RpcInfo info = default)
        => HostDepositAsync(info.Source, id, slotIndex, itemKey, amount).Forget();

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestWithdraw(int id, int slotIndex, int amount, RpcInfo info = default)
        => HostWithdraw(info.Source, id, slotIndex, amount);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestSwapSlots(int id, int fromIndex, int toIndex)
        => HostSwapSlots(id, fromIndex, toIndex);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestCooking(int id, NetworkBool start)
        => HostSetCooking(id, start);

    #endregion

    #region 호스트 처리

    private void HostPlace(PlayerRef requester, string itemKey, Vector3 position, Quaternion rotation)
    {
        if (!HasStateAuthority || string.IsNullOrEmpty(itemKey)) return;

        // 설치 불가 — 요청자가 이미 인벤토리에서 뺀 아이템을 돌려준다
        if (Structures.Count >= Structures.Capacity
            || !NetworkItemKeys.TryRegister(ItemKeyNames, itemKey, IsItemHashUsed, out int itemHash))
        {
            Debug.LogWarning($"[StructureDirector] 구조물을 더 설치할 수 없습니다(Capacity={StructureCapacity} 초과 또는 키 등록 실패): {itemKey}");
            Grant(requester, itemKey, 1, position);
            return;
        }

        int id = LastStructureId + 1;
        if (id <= 0) id = 1; // 오버플로 방지 (0은 "네트워크 구조물 아님")
        LastStructureId = id;

        Structures.Set(id, new StructureNetState
        {
            ItemHash = itemHash,
            Position = position,
            Rotation = rotation,
        });
    }

    // 철거(giveBack=false) 또는 줍기(giveBack=true). 보관함에 아이템이 남아 있으면 거부한다.
    private void HostRemove(PlayerRef requester, int id, bool giveBack)
    {
        if (!HasStateAuthority || !Structures.TryGet(id, out StructureNetState state)) return; // 이미 누가 치웠다
        if (HasStoredItems(id)) return;

        if (_views.TryGetValue(id, out View view) && view.Root != null)
        {
            Structure structure = view.Root.GetComponentInChildren<Structure>(true);
            if (structure != null && !structure.CanDemolish()) return;
        }

        string itemKey = NetworkItemKeys.Resolve(ItemKeyNames, state.ItemHash);
        RemoveStructureEntry(id);

        if (giveBack)
            Grant(requester, itemKey, 1, state.Position);
    }

    private async UniTaskVoid HostDepositAsync(PlayerRef requester, int id, int slotIndex, string itemKey, int amount)
    {
        if (!HasStateAuthority || amount <= 0) return;

        StorageStation storage = GetHostStorage(id);
        ItemDataSO itemData = storage != null ? await GetItemDataAsync(itemKey) : null;

        // 로드하는 사이 철거됐을 수 있어 다시 확인한다
        storage = GetHostStorage(id);
        if (storage == null || itemData == null)
        {
            Grant(requester, itemKey, amount, GetStructurePosition(id));
            return;
        }

        int remaining;
        if (slotIndex < 0)
            storage.AddItem(itemData, amount, out remaining);
        else
            storage.AddItemAt(itemData, slotIndex, amount, out remaining);

        if (remaining > 0)
            Grant(requester, itemKey, remaining, GetStructurePosition(id));
    }

    private void HostWithdraw(PlayerRef requester, int id, int slotIndex, int amount)
    {
        StorageStation storage = GetHostStorage(id);
        if (storage == null || slotIndex < 0 || slotIndex >= storage.SlotCount) return;

        ItemDataSO itemData = storage.Slots[slotIndex];
        int taken = Mathf.Min(amount, storage.StackCounts[slotIndex]);
        if (itemData == null || taken <= 0) return;

        if (!storage.RemoveItemAt(slotIndex, taken)) return;
        Grant(requester, itemData.name, taken, GetStructurePosition(id));
    }

    private void HostSwapSlots(int id, int fromIndex, int toIndex)
        => GetHostStorage(id)?.SwapOrMergeSlots(fromIndex, toIndex);

    private void HostSetCooking(int id, bool start)
    {
        if (!(GetHostStorage(id) is CookingPot pot)) return;

        if (start) pot.TryStartCooking();
        else pot.StopCooking();
    }

    private StorageStation GetHostStorage(int id)
    {
        if (!HasStateAuthority || !Structures.ContainsKey(id)) return null;
        return _views.TryGetValue(id, out View view) && view.Root != null ? view.Storage : null;
    }

    private Vector3 GetStructurePosition(int id)
        => Structures.TryGet(id, out StructureNetState state) ? state.Position : Vector3.zero;

    // 요청자 인벤토리에 지급한다 (호스트 자신이면 로컬 실행). 요청자가 나갔으면 바닥에 떨어뜨린다.
    private static void Grant(PlayerRef requester, string itemKey, int amount, Vector3 fallbackPosition)
    {
        if (string.IsNullOrEmpty(itemKey) || amount <= 0) return;

        NetworkPlayerData data = Main.Network != null ? Main.Network.GetPlayerData(requester) : null;
        if (data != null)
            data.Rpc_GrantPickup(itemKey, amount, string.Empty, -1f, -1f);
        else
            WorldItemSync.SpawnDroppedItem(itemKey, amount, fallbackPosition);
    }

    #endregion

    #region 아이템 데이터 캐시

    private async UniTask<ItemDataSO> GetItemDataAsync(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey)) return null;
        if (_itemCache.TryGetValue(itemKey, out ItemDataSO cached)) return cached;

        ItemDataSO itemData = await WorldItemSync.LoadItemDataAsync(itemKey);
        if (itemData != null) _itemCache[itemKey] = itemData;
        return itemData;
    }

    // 캐시에 있으면 즉시 반환. 없으면 로드를 걸어두고 false (끝나면 다시 반영하도록 _dirty를 켠다).
    private bool TryGetCachedItem(string itemKey, out ItemDataSO itemData)
    {
        if (_itemCache.TryGetValue(itemKey, out itemData)) return true;

        if (_loadingKeys.Add(itemKey))
            LoadIntoCacheAsync(itemKey).Forget();
        return false;
    }

    private async UniTaskVoid LoadIntoCacheAsync(string itemKey)
    {
        ItemDataSO itemData = await GetItemDataAsync(itemKey);
        if (itemData == null) _failedKeys.Add(itemKey);
        _loadingKeys.Remove(itemKey);
        _dirty = true;
    }

    #endregion
}
#endif

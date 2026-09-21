using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어가 직접 들고 있는 인벤토리입니다.
/// 플레이어 단위 인벤토리 슬롯과 장착 슬롯을 관리합니다.
/// </summary>
public partial class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 설정")]
    [SerializeField] private int slotCount = 20;
    [SerializeField] private int quickSlotCount = 0;

    [Header("줍기 설정")]
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask pickupLayer = ~0;
    [Tooltip("화면 중앙 조준으로 줍기/상호작용할 수 있는 거리(m, 카메라 기준). 카메라가 눈높이에 있어 바닥 아이템까지 닿도록 pickupRadius보다 길게 둔다")]
    [SerializeField] private float focusRange = 3f;

    [Header("Tool Use")]
    [SerializeField] private float defaultToolUseRange = 2.5f;
    [SerializeField] private LayerMask toolUseLayer = ~0;
    [Tooltip("맨손으로 나무/돌을 칠 때의 데미지 (도구보다 느리게 채집되도록 낮게 유지). 임시값.")]
    [SerializeField] private int bareHandDamage = 1;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();
    [SerializeField] private List<float> expirationTimestamps = new();

    [Header("부패 설정")]
    [Tooltip("expirationTime이 지난 스택이 전환될 아이템 (SpecialType.Rot)")]
    [SerializeField] private ItemDataSO rotItemSO;

    [Header("사망 드롭 설정")]
    [Tooltip("사망 시 소지품을 흩뿌릴 반경 (발밑 기준)")]
    [SerializeField] private float deathDropRadius = 1.5f;

    // 장착 슬롯 드롭 순서 (손 → 몸통 → 머리)
    private static readonly EquipSlot[] EquipDropOrder = { EquipSlot.Hand, EquipSlot.Chest, EquipSlot.Head };

    private readonly Dictionary<EquipSlot, IEquipable> equippedItemInstances = new();
    private readonly Collider[] pickupBuffer = new Collider[16];
    private readonly RaycastHit[] focusHits = new RaycastHit[16];
    private PlayerInputData inputData;
    private Player owner;
    private int selectedSlotIndex;
    private int hoveredSlotIndex = -1;
    private CharacterController characterController;

    // 화면 중앙으로 조준한 대상의 종류
    private enum FocusKind
    {
        None,
        Pickup,     // 주울 수 있는 월드 아이템 (G)
        HandPick,   // 맨손 채집 노드 — 풀 등 (G)
        Cook,       // 선택 슬롯의 날것을 구울 수 있는 모닥불 (우클릭)
        Structure,  // 상자·냉장고 등 열 수 있는 구조물 (E)
    }

    private struct PickupCandidate
    {
        public GameObject GameObject;
        public ItemDataSO ItemData;
        public int Amount;
    }

    // 슬롯을 비운 뒤 스폰을 기다리는 동안 들고 있을 드롭 정보
    private readonly struct PendingDrop
    {
        public readonly ItemDataSO ItemData;
        public readonly int Amount;
        public readonly ItemStackSaveData State;

        public PendingDrop(ItemDataSO itemData, int amount, ItemStackSaveData state)
        {
            ItemData = itemData;
            Amount = amount;
            State = state;
        }
    }

    public event Action OnInventoryChanged;
    public event Action<int> OnSelectedSlotChanged;
    public event Action<EquipSlot, ItemDataSO> OnEquippedItemChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;
    public int QuickSlotCount => GetQuickSlotCount();
    public int SelectedSlotIndex => selectedSlotIndex;
    public ItemDataSO EquippedHead { get; private set; }
    public ItemDataSO EquippedChest { get; private set; }
    public ItemDataSO EquippedHand { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        InitializeSlots();
    }

    public void Bind(Player owner, PlayerInputData data)
    {
        this.owner = owner;
        inputData = data;
        InitializeSlots();
    }

    private void OnDestroy()
    {
        ClearEquippedItemInstances();
    }

    public void Tick()
    {
        if (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return;
        CheckExpirations();
        TickEquippedTorchDurability();

        if (inputData == null) return;

        if (inputData.QuickSlotIndex >= 0)
            SelectSlot(inputData.QuickSlotIndex);

        if (inputData.QuickSlotScrollDelta != 0)
            MoveSelectedSlot(inputData.QuickSlotScrollDelta);

        if (inputData.PickupPressed)
        {
            if (!TryPickupFocused())
                TryPickupNearest();
        }

        // E: 보관함이 열려 있으면 조준 여부와 상관없이 닫기.
        // 아니면 조준한 구조물이 있을 때 열고, 없으면 기존처럼 선택 슬롯 장착.
        if (inputData.EquipSelectedPressed)
        {
            if (UI_Popup_Chest.Current != null)
                UI_Popup_Chest.Current.Close();
            else if (!TryInteractFocused())
                EquipSelectedSlot();
        }

        if (inputData.ToolUsePressed)
        {
            if (!TryCookAtBonfire() && !TryUseEquippedHandTool())
                TryBareHandHit();
        }

        if (inputData.DropPressed && hoveredSlotIndex >= 0)
            DropFromSlot(hoveredSlotIndex);
    }

    /// <summary>인벤토리 슬롯 UI가 마우스로 가리켜질 때 등록한다 (드롭 단축키 대상 지정용).</summary>
    public void SetHoveredSlot(int slotIndex)
    {
        hoveredSlotIndex = slotIndex;
    }

    /// <summary>가리키던 슬롯에서 마우스가 벗어나면 해제한다. 다른 슬롯이 이미 등록됐다면 무시한다.</summary>
    public void ClearHoveredSlot(int slotIndex)
    {
        if (hoveredSlotIndex == slotIndex)
            hoveredSlotIndex = -1;
    }

    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        SelectSlot(Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1));
        OnInventoryChanged?.Invoke();
    }

    public void SetQuickSlotCount(int count)
    {
        quickSlotCount = Mathf.Clamp(count, 0, slotCount);
        SelectSlot(Mathf.Clamp(selectedSlotIndex, 0, GetQuickSlotCount() - 1));
        OnInventoryChanged?.Invoke();
    }

    public bool AddItem(ItemDataSO itemData, int amount = 1)
    {
        return AddItem(itemData, amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount);
        OnInventoryChanged?.Invoke();
        return added;
    }

    /// <summary>
    /// 남은 소비기한(초)을 지정해서 넣는다. 보관함에서 꺼낸 아이템처럼 기한을 이어받아야 할 때 사용.
    /// remainingSeconds가 음수면 아이템 기본 소비기한으로 새로 시작한다.
    /// </summary>
    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount, float remainingSeconds)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount, -1f, remainingSeconds);
        OnInventoryChanged?.Invoke();
        return added;
    }

    /// <summary>
    /// 인벤토리에 있는 해당 아이템의 남은 소비기한(초) 중 가장 이른 값. 부패하지 않거나 없으면 -1.
    /// 보관함에 넣을 때 기한을 넘겨주기 위해 사용한다 (신선한 걸로 세탁하지 못하게 가장 이른 값을 준다).
    /// </summary>
    public float GetRemainingExpirationSeconds(ItemDataSO itemData)
    {
        if (itemData == null || itemData.expirationTime <= 0f) return -1f;

        float earliest = -1f;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != itemData || stackCounts[i] <= 0) continue;
            if (expirationTimestamps[i] <= 0f) continue;

            float remaining = Mathf.Max(0f, expirationTimestamps[i] - SavePlayClock.Now);
            if (earliest < 0f || remaining < earliest)
                earliest = remaining;
        }

        return earliest;
    }

    /// <summary>
    /// 인벤토리에 실제로 넣을 수 있는 수량 (최대 amount). AddItem과 같은 규칙으로 계산한다.
    /// 멀티에서 바닥 아이템을 줍기 전에 들어갈 만큼만 호스트에 요청할 때 사용.
    /// </summary>
    public int GetAddableAmount(ItemDataSO itemData, int amount)
    {
        if (itemData == null || amount <= 0) return 0;

        int maxStack = itemData.isStackable ? Mathf.Max(1, itemData.maxStack) : 1;
        int addable = 0;

        for (int i = 0; i < slots.Count && addable < amount; i++)
        {
            if (slots[i] == null)
                addable += maxStack;
            else if (itemData.isStackable && slots[i] == itemData)
                // 같은 아이템인데 수량이 0으로 남은 슬롯은 AddItem이 비운 뒤 새로 채운다
                addable += stackCounts[i] <= 0 ? maxStack : Mathf.Max(0, maxStack - stackCounts[i]);
        }

        return Mathf.Min(addable, amount);
    }

    public bool RemoveItem(ItemDataSO itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;
        if (!HasItem(itemData, amount)) return false;

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i] == itemData && stackCounts[i] <= 0)
            {
                ClearSlot(i);
                continue;
            }

            if (slots[i] != itemData) continue;

            int removeAmount = Mathf.Min(stackCounts[i], amount);
            stackCounts[i] -= removeAmount;
            amount -= removeAmount;

            if (stackCounts[i] <= 0)
                ClearSlot(i);

            if (amount <= 0)
            {
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    public bool HasItem(ItemDataSO itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    public int GetItemCount(ItemDataSO itemData)
    {
        if (itemData == null) return 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] > 0)
                count += stackCounts[i];

        return count;
    }

    /// <summary>슬롯의 Food/Dish 아이템, 또는 회복 수치가 설정된 생자원(열매 등)을
    /// 1개 먹어서 허기/체력/Ego를 회복하고 소모한다.</summary>
    public bool EatFromSlot(int slotIndex)
    {
        if (Application.isPlaying && Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return false;
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        if (itemData == null || stackCounts[slotIndex] <= 0) return false;
        if (!IsEdible(itemData)) return false;
        if (owner == null || owner.Stat == null) return false;

        owner.Stat.RestoreHunger(itemData.hungerRestore);
        owner.Stat.RestoreHp(itemData.healthRestore);
        owner.Stat.RestoreEgo(itemData.egoRestore);

        return RemoveItem(itemData, 1);
    }

    /// <summary>Food/Dish는 항상 먹을 수 있고, 자원(Resource)은 회복 수치가 하나라도 설정돼 있으면 생으로 먹을 수 있다.</summary>
    private static bool IsEdible(ItemDataSO itemData)
    {
        if (itemData.itemType == ItemType.Food || itemData.itemType == ItemType.Dish)
            return true;

        return itemData.itemType == ItemType.Resource
            && (itemData.hungerRestore != 0 || itemData.healthRestore != 0 || itemData.egoRestore != 0);
    }

    /// <summary>슬롯의 아이템 전체(스택 통째로)를 플레이어 앞 땅에 드롭한다.</summary>
    public bool DropFromSlot(int slotIndex)
    {
        if (Application.isPlaying && Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return false;
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        int amount = stackCounts[slotIndex];
        if (itemData == null || amount <= 0) return false;

        // 스폰 대기 중에 플레이어가 파괴될 수 있으므로 위치를 미리 계산해 둔다.
        Vector3 dropPosition = GetDropOriginPosition() + transform.forward * 1.5f;
        var saved = CaptureSlot(slotIndex);

        ClearSlot(slotIndex);
        OnInventoryChanged?.Invoke();

        SpawnDroppedItem(itemData, amount, dropPosition, saved);
        return true;
    }

    /// <summary>
    /// 인벤토리 슬롯과 장착 슬롯의 모든 아이템을 발밑에 흩뿌린다. (사망 시 호출)
    /// </summary>
    /// <returns>드롭된 스택 수</returns>
    public int DropAll()
    {
        List<PendingDrop> pending = new();

        // 장착 아이템은 슬롯으로 되돌리지 않고 곧바로 월드에 떨군다.
        // (인벤토리를 비우는 중이라 되돌릴 공간을 따질 필요가 없다)
        for (int i = 0; i < EquipDropOrder.Length; i++)
        {
            EquipSlot equipSlot = EquipDropOrder[i];
            ItemDataSO equipped = GetEquippedItem(equipSlot);
            if (equipped == null) continue;

            var saved = CaptureEquipment(equipSlot);
            if (ClearEquippedItem(equipSlot))
                pending.Add(new PendingDrop(equipped, 1, saved));
        }

        // 인벤토리 슬롯 전체
        for (int i = 0; i < slots.Count; i++)
        {
            ItemDataSO itemData = slots[i];
            int amount = stackCounts[i];
            if (itemData == null || amount <= 0) continue;

            var saved = CaptureSlot(i);
            ClearSlot(i);
            pending.Add(new PendingDrop(itemData, amount, saved));
        }

        if (pending.Count <= 0) return 0;

        OnInventoryChanged?.Invoke();
        ScatterDrops(pending, GetDropOriginPosition());
        return pending.Count;
    }

    // 여러 스택을 한 점에 겹쳐 쌓지 않도록 발밑 반경 안에 흩뿌린다.
    private void ScatterDrops(List<PendingDrop> drops, Vector3 origin)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * deathDropRadius;
            Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);

            SpawnDroppedItem(drops[i].ItemData, drops[i].Amount, position, drops[i].State);
        }
    }

    // 아이템 주소는 SO 이름과 같다. 멀티에서는 호스트를 거쳐 모든 피어에 같은 바닥 아이템이 생긴다.
    private static void SpawnDroppedItem(ItemDataSO itemData, int amount, Vector3 position, ItemStackSaveData saved)
    {
        WorldItemSync.SpawnDroppedItem(itemData.name, amount, position, saved);
    }

    /// <summary>드롭 아이템이 놓일 기준 지점(플레이어 발밑).</summary>
    private Vector3 GetDropOriginPosition()
    {
        Vector3 feetPosition = transform.position;
        if (characterController != null)
            feetPosition.y += characterController.center.y - characterController.height * 0.5f;

        return feetPosition;
    }

    public bool EquipFromSlot(int slotIndex)
    {
        if (Application.isPlaying && Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return false;
        if (!IsValidSlot(slotIndex)) return false;

        ItemDataSO itemData = slots[slotIndex];
        if (itemData == null || itemData.equipSlot == EquipSlot.None || stackCounts[slotIndex] <= 0)
        {
            if (IsValidSlot(slotIndex) && stackCounts[slotIndex] <= 0)
            {
                ClearSlot(slotIndex);
                OnInventoryChanged?.Invoke();
            }
            return false;
        }

        ItemDataSO currentEquipped = GetEquippedItem(itemData.equipSlot);
        if (currentEquipped == itemData)
        {
            OnInventoryChanged?.Invoke();
            return false;
        }

        // 인벤토리 공간 확인 (실제 제거 전에 먼저 체크)
        if (currentEquipped != null)
        {
            int emptyCount = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null) emptyCount++;

            bool selectedSlotWillBeEmpty = stackCounts[slotIndex] <= 1;
            bool canReturnCurrent = emptyCount > 0 || selectedSlotWillBeEmpty || CanStackItem(currentEquipped);

            if (!canReturnCurrent)
            {
                // 빈 슬롯 없음 + 다른 아이템 → 교체 불가
                OnInventoryChanged?.Invoke();
                return false;
            }
        }

        var nextState = CaptureSlot(slotIndex);
        var previousState = CaptureEquipment(itemData.equipSlot);
        RemoveOneFromSlot(slotIndex);

        // 기존 장착 아이템을 인벤토리로 돌려보내기
        if (currentEquipped != null)
            TryAddItemToSlots(currentEquipped, 1, out _, previousState.durability, previousState.spoilRemainingSeconds);

        // 교체 시 기존 아이템 해제 이벤트 명시적으로 발생
        if (currentEquipped != null && currentEquipped != itemData)
            OnEquippedItemChanged?.Invoke(itemData.equipSlot, null); // 해제 알림

        SetEquippedItem(itemData.equipSlot, itemData, nextState.durability, nextState.spoilRemainingSeconds);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool EquipSelectedSlot()
    {
        return EquipFromSlot(selectedSlotIndex);
    }

    public void SelectSlot(int index)
    {
        if (slots.Count == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, GetQuickSlotCount() - 1);
        if (selectedSlotIndex == clampedIndex) return;

        selectedSlotIndex = clampedIndex;
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
        OnInventoryChanged?.Invoke();
    }

    public void MoveSelectedSlot(int delta)
    {
        if (slots.Count == 0 || delta == 0) return;

        int count = GetQuickSlotCount();
        int nextIndex = (selectedSlotIndex + delta) % count;
        if (nextIndex < 0)
            nextIndex += count;

        SelectSlot(nextIndex);
    }

    public bool EquipItem(ItemDataSO itemData)
    {
        if (Application.isPlaying && Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return false;
        if (itemData == null || itemData.equipSlot == EquipSlot.None) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == itemData)
                return EquipFromSlot(i);
        }

        return false;
    }

    public bool UnequipItem(EquipSlot equipSlot)
    {
        if (Application.isPlaying && Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return false;
        if (equipSlot == EquipSlot.None) return false;

        ItemDataSO itemData = GetEquippedItem(equipSlot);
        if (itemData == null) return false;

        // 인벤토리 공간 먼저 확인 (상태 변경 전)
        if (GetEmptySlotIndex() < 0 && !CanStackItem(itemData))
        {
            OnInventoryChanged?.Invoke();
            return false;
        }

        // 공간 확인 후 한 번에 처리
        var saved = CaptureEquipment(equipSlot);
        SetEquippedItem(equipSlot, null);
        TryAddItemToSlots(itemData, 1, out _, saved.durability, saved.spoilRemainingSeconds);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void RegisterEquippedItemInstance(EquipSlot equipSlot, IEquipable equipable)
    {
        if (equipSlot == EquipSlot.None) return;

        UnsubscribeEquippedItemInstance(equipSlot);

        if (equipable == null) return;

        equippedItemInstances[equipSlot] = equipable;
        equipable.OnBroken += OnEquippedItemBroken;
    }

    public void UnregisterEquippedItemInstance(EquipSlot equipSlot, IEquipable equipable)
    {
        if (equipSlot == EquipSlot.None) return;
        if (!equippedItemInstances.TryGetValue(equipSlot, out IEquipable current)) return;
        if (current != equipable) return;

        UnsubscribeEquippedItemInstance(equipSlot);
    }

    public IEquipable GetEquippedItemInstance(EquipSlot equipSlot)
    {
        return equippedItemInstances.TryGetValue(equipSlot, out IEquipable equipable) ? equipable : null;
    }

    // 헬퍼 추가
    private bool CanStackItem(ItemDataSO itemData)
    {
        if (!itemData.isStackable) return false;
        int maxStack = Mathf.Max(1, itemData.maxStack);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] < maxStack)
                return true;
        return false;
    }


    public bool ClearEquippedItem(EquipSlot equipSlot, ItemDataSO expectedItem = null)
    {
        if (equipSlot == EquipSlot.None) return false;

        ItemDataSO itemData = GetEquippedItem(equipSlot);
        if (itemData == null) return false;
        if (expectedItem != null && itemData != expectedItem) return false;

        SetEquippedItem(equipSlot, null);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public ItemDataSO GetEquippedItem(EquipSlot equipSlot) => equipSlot switch
    {
        EquipSlot.Head => EquippedHead,
        EquipSlot.Chest => EquippedChest,
        EquipSlot.Hand => EquippedHand,
        _ => null
    };

    private void InitializeSlots()
    {
        if (slotCount < 1) slotCount = 1;

        // 부족한 슬롯만 뒤에 추가
        while (slots.Count < slotCount)
            slots.Add(null);

        while (stackCounts.Count < slotCount)
            stackCounts.Add(0);

        while (expirationTimestamps.Count < slotCount)
            expirationTimestamps.Add(0f);
        while (slotDurabilities.Count < slotCount)
            slotDurabilities.Add(-1f);

        // 초과 슬롯은 뒤에서 제거
        while (slots.Count > slotCount)
            slots.RemoveAt(slots.Count - 1);

        while (stackCounts.Count > slotCount)
            stackCounts.RemoveAt(stackCounts.Count - 1);

        while (expirationTimestamps.Count > slotCount)
            expirationTimestamps.RemoveAt(expirationTimestamps.Count - 1);
        while (slotDurabilities.Count > slotCount)
            slotDurabilities.RemoveAt(slotDurabilities.Count - 1);

        // 기존 데이터 보존: stackCount가 0이면 maxStack 기준으로 복원
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null)
            {
                stackCounts[i] = 0;
                continue;
            }

            // stackCount가 0이면 날리지 말고, 프리펩 설정값으로 보정
            if (stackCounts[i] <= 0)
                stackCounts[i] = 1;  // 최소 1개로 복원
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1);
    }
    private int GetQuickSlotCount()
    {
        if (slots.Count == 0) return 0;
        if (quickSlotCount <= 0) return slots.Count;
        return Mathf.Clamp(quickSlotCount, 1, slots.Count);
    }

    private void FillExistingStacks(ItemDataSO itemData, ref int remainingAmount, float newDeadline = -1f)
    {
        int maxStack = Mathf.Max(1, itemData.maxStack);

        for (int i = 0; i < slots.Count && remainingAmount > 0; i++)
        {
            if (slots[i] != itemData) continue;
            if (stackCounts[i] <= 0)
            {
                ClearSlot(i);
                continue;
            }

            int space = maxStack - stackCounts[i];
            if (space <= 0) continue;

            int addAmount = Mathf.Min(space, remainingAmount);
            stackCounts[i] += addAmount;
            remainingAmount -= addAmount;

            if (newDeadline > 0f)
            {
                expirationTimestamps[i] = expirationTimestamps[i] > 0f
                    ? Mathf.Min(expirationTimestamps[i], newDeadline)
                    : newDeadline;
            }
        }
    }

    private bool TryAddItemToSlots(ItemDataSO itemData, int amount, out int remainingAmount,
        float durability = -1f, float spoilRemainingSeconds = -1f)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        // remainingSeconds가 음수면 아이템 기본 소비기한으로 새로 시작한다.
        float deadline = 0f;
        if (itemData.expirationTime > 0f)
        {
            float seconds = spoilRemainingSeconds >= 0f ? spoilRemainingSeconds : itemData.expirationTime * 60f;
            deadline = SavePlayClock.Now + seconds;
        }

        if (itemData.isStackable)
            FillExistingStacks(itemData, ref remainingAmount, deadline);

        while (remainingAmount > 0)
        {
            int emptyIndex = GetEmptySlotIndex();
            if (emptyIndex < 0)
                return false;

            int stackSize = itemData.isStackable
                ? Mathf.Min(remainingAmount, Mathf.Max(1, itemData.maxStack))
                : 1;

            slots[emptyIndex] = itemData;
            stackCounts[emptyIndex] = stackSize;
            slotDurabilities[emptyIndex] = itemData.hasDurability
                ? (durability < 0f ? itemData.maxDurability : durability) : -1f;
            expirationTimestamps[emptyIndex] = deadline;
            remainingAmount -= stackSize;
        }

        return true;
    }

    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                return i;

        return -1;
    }

    private bool IsValidSlot(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    private void RemoveOneFromSlot(int index)
    {
        if (!IsValidSlot(index) || slots[index] == null) return;

        stackCounts[index]--;
        if (stackCounts[index] <= 0)
            ClearSlot(index);
    }

    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
        expirationTimestamps[index] = 0f;
        slotDurabilities[index] = -1f;
    }

    /// <summary>소비기한이 지난 스택을 rotItemSO로 전환한다.</summary>
    private void CheckExpirations()
    {
        if (rotItemSO == null) return;

        bool anyExpired = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || stackCounts[i] <= 0) continue;
            if (expirationTimestamps[i] <= 0f || SavePlayClock.Now < expirationTimestamps[i]) continue;

            int rotAmount = stackCounts[i];
            ClearSlot(i);
            TryAddItemToSlots(rotItemSO, rotAmount, out _);
            anyExpired = true;
        }

        if (anyExpired)
            OnInventoryChanged?.Invoke();
    }

    /// <summary>손에 든 횃불이 켜져있는 동안 시간 경과에 따라 내구도를 소모시킨다.</summary>
    private void TickEquippedTorchDurability()
    {
        if (EquippedHand == null || EquippedHand.survivalToolType != SurvivalToolType.Torch) return;

        IEquipable torch = GetEquippedItemInstance(EquipSlot.Hand);
        torch?.DrainDurabilityOverTime(Time.deltaTime);
    }

    private void SetEquippedItem(EquipSlot equipSlot, ItemDataSO itemData,
        float durability = -1f, float spoilRemainingSeconds = -1f)
    {
        UnsubscribeEquippedItemInstance(equipSlot);
        equippedSpoilDeadlines.Remove(equipSlot);
        if (itemData != null)
        {
            var runtime = ItemData.CreateFromSO(itemData) as ItemData_Equipable;
            if (runtime != null)
            {
                runtime.RestoreDurability(durability);
                RegisterEquippedItemInstance(equipSlot, runtime);
            }
            equippedSpoilDeadlines[equipSlot] = spoilRemainingSeconds < 0f ? -1f : SavePlayClock.Now + spoilRemainingSeconds;
        }

        switch (equipSlot)
        {
            case EquipSlot.Head:
                EquippedHead = itemData;
                break;
            case EquipSlot.Chest:
                EquippedChest = itemData;
                break;
            case EquipSlot.Hand:
                EquippedHand = itemData;
                break;
        }

        // 손 장비는 1인칭 뷰가 프리팹을 띄우며 인스턴스를 등록하지만,
        // 머리/몸통 방어구는 띄울 프리팹이 없으므로 여기서 내구도 인스턴스를 직접 만든다.
        if (itemData != null && equipSlot != EquipSlot.Hand)
            RegisterEquippedItemInstance(equipSlot, ItemData.CreateFromSO(itemData) as IEquipable);

        OnEquippedItemChanged?.Invoke(equipSlot, itemData);
    }

    #region Armor

    // 방어구 방어력을 피해 감소율(%)로 읽는 슬롯. 방패는 손 슬롯이다.
    private static readonly EquipSlot[] ArmorSlots = { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Hand };

    /// <summary>
    /// 장착 방어구로 인한 받는 피해 배율 (1 = 감소 없음).
    /// 방어구의 defense는 피해 감소율(%)로 취급하고, 여러 부위는 곱연산으로 겹친다.
    /// 예) 투구 15 + 갑옷 30 → 0.85 × 0.70 = 0.595
    /// </summary>
    public float GetArmorDamageMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < ArmorSlots.Length; i++)
        {
            if (!TryGetUsableArmor(ArmorSlots[i], out ItemDataSO armor, out _)) continue;
            multiplier *= 1f - Mathf.Clamp(armor.defense, 0f, 95f) * 0.01f;
        }

        return multiplier;
    }

    /// <summary>피격 시 장착한 방어구들의 내구도를 한 번씩 깎는다. 다 닳으면 OnBroken으로 장착 해제된다.</summary>
    public void ConsumeArmorDurabilityOnHit()
    {
        for (int i = 0; i < ArmorSlots.Length; i++)
        {
            if (TryGetUsableArmor(ArmorSlots[i], out _, out IEquipable instance))
                instance?.UseDurability();
        }
    }

    // 방어력이 있는 전투장비(투구/갑옷/방패)이고 부서지지 않았으면 true.
    private bool TryGetUsableArmor(EquipSlot slot, out ItemDataSO armor, out IEquipable instance)
    {
        armor = GetEquippedItem(slot);
        instance = GetEquippedItemInstance(slot);

        if (armor == null || armor.itemType != ItemType.CombatGear || armor.defense <= 0f)
            return false;

        return instance == null || instance.IsUsable;
    }

    #endregion

    private void OnEquippedItemBroken(IEquipable brokenItem)
    {
        if (brokenItem == null) return;

        EquipSlot brokenSlot = EquipSlot.None;
        foreach (KeyValuePair<EquipSlot, IEquipable> pair in equippedItemInstances)
        {
            if (pair.Value != brokenItem) continue;

            brokenSlot = pair.Key;
            break;
        }

        if (brokenSlot == EquipSlot.None) return;

        UnsubscribeEquippedItemInstance(brokenSlot);
        ClearEquippedItem(brokenSlot, brokenItem.ItemSO);
    }

    private void UnsubscribeEquippedItemInstance(EquipSlot equipSlot)
    {
        if (!equippedItemInstances.TryGetValue(equipSlot, out IEquipable equipable))
            return;

        equipable.OnBroken -= OnEquippedItemBroken;
        equippedItemInstances.Remove(equipSlot);
    }

    private void ClearEquippedItemInstances()
    {
        if (equippedItemInstances.Count <= 0) return;

        List<EquipSlot> slotsToClear = new(equippedItemInstances.Keys);
        for (int i = 0; i < slotsToClear.Count; i++)
            UnsubscribeEquippedItemInstance(slotsToClear[i]);
    }


    /// <summary>크로스헤어가 줍기/상호작용 가능한 대상(월드 아이템, 맨손 채집 노드, 요리 가능한 모닥불)을 조준 중인지 (UI 포커스 판정용).</summary>
    public bool HasInteractTargetFocused()
        => TryGetFocusedTarget(out _, out _);

    private bool TryPickupFocused()
    {
        if (!TryGetFocusedTarget(out RaycastHit hit, out FocusKind kind))
            return false;

        switch (kind)
        {
            // 맨손 채집 노드 (풀 등) — G키로 즉시 채집
            case FocusKind.HandPick:
                hit.collider.GetComponentInParent<ResourceNode>()
                    .HandPick(new DamageContext(gameObject, hit.point, 0, "Hand", ActionType.Hand));
                return true;

            case FocusKind.Pickup:
                if (!TryGetPickupCandidate(hit.collider, out PickupCandidate candidate)) return false;
                PickupWorldItem(candidate);
                return true;

            default:
                return false; // 요리는 G가 아니라 우클릭(TryCookAtBonfire)
        }
    }

    /// <summary>
    /// 화면 중앙 레이에 걸린 것 중 가장 가까운 줍기/상호작용 대상을 찾는다.
    /// 대상이 아닌 트리거(스테이션 근접 범위 등)와 자기 캐릭터 콜라이더는 통과하고, 대상이 아닌 실제 물체(벽·지형)에 막히면 멈춘다.
    /// 포커스 표시와 G키 줍기가 같은 판정을 써서 점이 켜진 대상이 곧 주워지는 대상이 되도록 한다.
    /// </summary>
    private bool TryGetFocusedTarget(out RaycastHit targetHit, out FocusKind kind)
    {
        targetHit = default;
        kind = FocusKind.None;

        Camera camera = Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, focusHits, focusRange, pickupLayer, QueryTriggerInteraction.Collide);
        Array.Sort(focusHits, 0, count, RaycastDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = focusHits[i];
            Collider col = hit.collider;
            if (col == null) continue;

            // 자기 캐릭터(1인칭 뷰모델 포함) 콜라이더는 무시
            if (col.transform.IsChildOf(transform)) continue;

            kind = ClassifyFocus(col);
            if (kind != FocusKind.None)
            {
                targetHit = hit;
                return true;
            }

            if (!col.isTrigger) return false; // 벽/지형 등 실제 물체에 막힘
        }

        return false;
    }

    private FocusKind ClassifyFocus(Collider col)
    {
        // 상자·작업대·모닥불 같은 설치된 구조물은 Item을 겸해서 갖고 있어도
        // "줍기"보다 "상호작용(E)"이 항상 우선이어야 한다 — 먼저 판정한다.
        Structure structure = col.GetComponentInParent<Structure>();
        if (structure != null && structure.CanInteract(BuildInteractionContext(structure.transform.position)))
            return FocusKind.Structure;

        if (TryGetPickupCandidate(col, out _))
            return FocusKind.Pickup;

        ResourceNode node = col.GetComponentInParent<ResourceNode>();
        if (node != null && node.IsHandPickable)
            return FocusKind.HandPick;

        BonfireCooker bonfire = col.GetComponentInParent<BonfireCooker>();
        if (bonfire != null && bonfire.CanCookFromInventory(this))
            return FocusKind.Cook;

        return FocusKind.None;
    }

    private InteractionContext BuildInteractionContext(Vector3 point, Vector3 normal = default)
        => new InteractionContext(gameObject, point, normal);

    /// <summary>
    /// 조준한 구조물(상자·냉장고 등)을 연다. 연 경우 true.
    /// E키는 대상이 있으면 상호작용, 없으면 기존처럼 선택 슬롯 장착으로 동작한다.
    /// </summary>
    private bool TryInteractFocused()
    {
        if (!TryGetFocusedTarget(out RaycastHit hit, out FocusKind kind)) return false;
        if (kind != FocusKind.Structure) return false;

        Structure structure = hit.collider.GetComponentInParent<Structure>();
        if (structure == null) return false;

        InteractionContext ctx = BuildInteractionContext(hit.point, hit.normal);
        if (!structure.CanInteract(ctx)) return false;

        structure.Interact(ctx);
        return true;
    }

    private sealed class RaycastDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastDistanceComparer Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private void TryPickupNearest()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            pickupRadius,
            pickupBuffer,
            pickupLayer,
            QueryTriggerInteraction.Collide);

        PickupCandidate nearest = default;
        bool hasNearest = false;
        float nearestSqrDistance = float.MaxValue;
        ResourceNode nearestNode = null;
        float nearestNodeSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = pickupBuffer[i];
            if (col == null) continue;

            if (TryGetPickupCandidate(col, out PickupCandidate candidate))
            {
                float sqrDistance = (candidate.GameObject.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearest = candidate;
                    hasNearest = true;
                    nearestSqrDistance = sqrDistance;
                }
                continue;
            }

            ResourceNode node = col.GetComponentInParent<ResourceNode>();
            if (node != null && node.IsHandPickable)
            {
                float sqrDistance = (node.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < nearestNodeSqrDistance)
                {
                    nearestNode = node;
                    nearestNodeSqrDistance = sqrDistance;
                }
            }
        }

        // 아이템이 더 가깝거나 노드가 없으면 아이템 우선
        if (hasNearest && (nearestNode == null || nearestSqrDistance <= nearestNodeSqrDistance))
        {
            PickupWorldItem(nearest);
            return;
        }

        if (nearestNode != null)
            nearestNode.HandPick(new DamageContext(gameObject, nearestNode.transform.position, 0, "Hand", ActionType.Hand));
    }

    private void PickupWorldItem(PickupCandidate candidate)
    {
        Item worldItem = candidate.GameObject.GetComponent<Item>();

        // 멀티: 바닥/월드 배치 아이템은 호스트 승인 후 지급된다 (다른 플레이어와 동시에 주워도 한 번만)
        if (WorldItemSync.TryRequestPickup(this, worldItem))
            return;

        var saved = worldItem != null ? worldItem.CaptureSaveData() : null;
        bool added = AddItem(candidate.ItemData, candidate.Amount, out int remainingAmount,
            saved?.durability ?? -1f, saved?.spoilRemainingSeconds ?? -1f);
        ApplyPickupResult(candidate, remainingAmount);

        if (remainingAmount <= 0)
            WorldItemSync.RemovePickedItem(worldItem);
    }

    private static bool TryGetPickupCandidate(Collider col, out PickupCandidate candidate)
    {
        Item worldItem = col.GetComponentInParent<Item>();
        if (worldItem != null && worldItem.ItemDataSO != null)
        {
            candidate = new PickupCandidate
            {
                GameObject = worldItem.gameObject,
                ItemData = worldItem.ItemDataSO,
                Amount = GetWorldItemStackCount(worldItem)
            };
            return true;
        }

        candidate = default;
        return false;
    }

    private static void ApplyPickupResult(PickupCandidate candidate, int remainingAmount)
    {
        Item worldItem = candidate.GameObject.GetComponent<Item>();
        if (worldItem != null && worldItem.itemData is IStackable stackable)
            stackable.stackCount = remainingAmount;
    }

    private static int GetWorldItemStackCount(Item worldItem)
    {
        if (worldItem != null && worldItem.itemData is IStackable stackable)
            return Mathf.Max(1, stackable.stackCount);

        return 1;
    }

    /// <summary>
    /// 손에 든 아이템과 조준 대상으로 수행할 ActionType을 반환한다. 유효 타겟이 있을 때만 true.
    /// 몬스터·동물을 조준하면 무엇을 들었든(도구/무기) Attack, 그 외에는 도구 고유 액션(벌목/채굴 등).
    /// </summary>
    public bool TryGetToolActionType(out ActionType actionType)
    {
        actionType = ActionType.None;

        if (!TryGetUsableHandItem(out ItemDataSO handItem, out _))
            return false;

        if (!TryRaycastToolTarget(GetHandRange(handItem), out RaycastHit hit))
            return false;

        actionType = ResolveHandAction(hit, handItem);
        if (actionType == ActionType.None)
            return false;

        return TryDamageHitTarget(hit, handItem, actionType, apply: false);
    }

    public void UseEquippedHandTool()
    {
        TryUseEquippedHandTool();
    }

    private static ActionType GetActionTypeForTool(SurvivalToolType toolType) => toolType switch
    {
        SurvivalToolType.Axe_Stone or SurvivalToolType.Axe_Iron or SurvivalToolType.Axe_Gold         => ActionType.Chop,
        SurvivalToolType.Pickaxe_Stone or SurvivalToolType.Pickaxe_Iron or SurvivalToolType.Pickaxe_Gold => ActionType.Mine,
        SurvivalToolType.Shovel_Stone or SurvivalToolType.Shovel_Iron or SurvivalToolType.Shovel_Gold => ActionType.Dig,
        SurvivalToolType.Hammer_Stone or SurvivalToolType.Hammer_Iron or SurvivalToolType.Hammer_Gold => ActionType.Build,
        _ => ActionType.None,
    };

    private bool TryCookAtBonfire()
    {
        if (!TryRaycastToolTarget(defaultToolUseRange + 1f, out RaycastHit hit)) return false;
        BonfireCooker bonfire = hit.collider.GetComponentInParent<BonfireCooker>();
        if (bonfire == null) return false;
        return bonfire.TryCookFromInventory(this);
    }

    /// <summary>
    /// 손에 든 도구/무기로 조준 대상을 친다 (몬스터·동물 공격 또는 채집).
    /// 손이 비었으면 false를 반환해 맨손 채집으로 넘긴다. 무기로 몬스터·동물 외의 것을 조준해도 맨손 채집으로 넘긴다.
    /// </summary>
    private bool TryUseEquippedHandTool()
    {
        ItemDataSO handItem = EquippedHand;
        if (!IsHandUsableItem(handItem))
            return false;

        IEquipable handTool = GetEquippedItemInstance(EquipSlot.Hand);
        if (handTool != null && !handTool.IsUsable)
            return true;

        if (!TryRaycastToolTarget(GetHandRange(handItem), out RaycastHit hit))
            return true;

        ActionType actionType = ResolveHandAction(hit, handItem);
        if (actionType == ActionType.None)
            return handItem.itemType == ItemType.SurvivalTool;

        if (!TryDamageHitTarget(hit, handItem, actionType, apply: true))
            return true;

        handTool?.UseDurability();
        return true;
    }

    // 공격/도구 사용에 쓸 수 있는 손 아이템 종류 (생존도구·전투장비)
    private static bool IsHandUsableItem(ItemDataSO item)
        => item != null && (item.itemType == ItemType.SurvivalTool || item.itemType == ItemType.CombatGear);

    // 손에 쓸 수 있는 아이템이 있고 부서지지 않았으면 true.
    private bool TryGetUsableHandItem(out ItemDataSO handItem, out IEquipable handTool)
    {
        handItem = EquippedHand;
        handTool = GetEquippedItemInstance(EquipSlot.Hand);

        if (!IsHandUsableItem(handItem)) return false;
        return handTool == null || handTool.IsUsable;
    }

    private float GetHandRange(ItemDataSO handItem)
        => Mathf.Max(defaultToolUseRange, handItem.attackRange);

    // 몬스터·동물이면 무엇을 들었든 공격. 아니면 생존도구 고유 액션 (전투장비는 없음).
    private static ActionType ResolveHandAction(RaycastHit hit, ItemDataSO handItem)
    {
        if (IsCombatTarget(hit.collider))
            return ActionType.Attack;

        return handItem.itemType == ItemType.SurvivalTool
            ? GetActionTypeForTool(handItem.survivalToolType)
            : ActionType.None;
    }

    private static bool IsCombatTarget(Collider collider)
        => collider.GetComponentInParent<Monster>() != null || collider.GetComponentInParent<Animal>() != null;

    /// <summary>
    /// 전투 데미지 = 손 아이템 공격력 + 플레이어 기본 공격력. (도구는 공격력이 낮아 기본 공격력이 바닥을 받쳐준다)
    /// </summary>
    private DamageContext CreateCombatContext(RaycastHit hit, ItemDataSO handItem, ActionType actionType)
    {
        float baseAttack = owner != null && owner.Stat != null ? owner.Stat.Attack : 0f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(handItem.attackDamage + baseAttack));
        return new DamageContext(gameObject, hit.point, damage, handItem.itemID, actionType, handItem.harvestableNodeTypes);
    }

    /// <summary>도구 없이 맨손으로 나무/돌 자원 노드를 느리게 친다.</summary>
    private void TryBareHandHit()
    {
        if (!TryRaycastToolTarget(defaultToolUseRange, out RaycastHit hit))
            return;

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node == null) return;

        // 맨손 채집은 나무/돌 노드로 한정한다.
        if (node.NodeType != ResourceNodeType.Tree && node.NodeType != ResourceNodeType.Mine)
            return;

        var ctx = new DamageContext(gameObject, hit.point, Mathf.Max(1, bareHandDamage), "Hand", ActionType.Hand);
        if (node.CanDamage(ctx))
            node.ApplyDamage(ctx);
    }

    /// <summary>
    /// 레이캐스트 히트를 데미지 가능한 대상(자원 채집 노드, 몬스터, 망치로 부술 구조물)으로 해석한다.
    /// apply가 true면 실제로 데미지를 적용하고, false면 가능 여부만 확인한다(UI 판정용).
    /// </summary>
    private bool TryDamageHitTarget(RaycastHit hit, ItemDataSO handItem, ActionType actionType, bool apply)
    {
        if (actionType == ActionType.Build)
        {
            // 철거는 StructureSync를 거친다 (멀티에서는 호스트가 확정해 모든 피어에서 사라진다)
            Structure structure = hit.collider.GetComponentInParent<Structure>();
            if (structure != null)
            {
                if (!StructureSync.CanDemolish(structure.gameObject)) return false;
                if (apply) StructureSync.Demolish(structure.gameObject);
                return true;
            }
            Item placedItem = hit.collider.GetComponentInParent<Item>();
            if (placedItem != null && placedItem.ItemDataSO != null && placedItem.ItemDataSO.itemType == ItemType.Structure)
            {
                if (apply) StructureSync.Demolish(placedItem.gameObject);
                return true;
            }
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(handItem.attackDamage));
        var ctx = new DamageContext(gameObject, hit.point, damage, handItem.itemID, actionType, handItem.harvestableNodeTypes);

        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            if (!node.CanDamage(ctx)) return false;
            if (apply) node.ApplyDamage(ctx);
            return true;
        }

        Monster monster = hit.collider.GetComponentInParent<Monster>();
        if (monster != null)
        {
            DamageContext combatCtx = CreateCombatContext(hit, handItem, actionType);
            if (!monster.CanDamage(combatCtx)) return false;
            if (apply) DamageMonster(monster, combatCtx, handItem, actionType);
            return true;
        }

        Animal animal = hit.collider.GetComponentInParent<Animal>();
        if (animal != null)
        {
            DamageContext combatCtx = CreateCombatContext(hit, handItem, actionType);
            if (!animal.CanDamage(combatCtx)) return false;
            if (apply) DamageAnimal(animal, combatCtx, handItem, actionType);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 몬스터에 데미지를 넣는다. 멀티플레이에서는 클라가 직접 깎지 않고 호스트에 보고한다.
    /// (몬스터의 HP/사망 확정은 호스트 전담 — NetworkMonsterDirector 참고)
    /// </summary>
    private void DamageMonster(Monster monster, DamageContext ctx, ItemDataSO handItem, ActionType actionType)
    {
#if PHOTON_FUSION
        var director = NetworkMonsterDirector.Instance;
        if (director != null)
        {
            director.ReportDamage(monster, ctx.Amount, handItem.itemID, actionType);
            return;
        }
#endif
        // 싱글플레이: 디렉터가 없으므로 그대로 로컬 적용
        monster.ApplyDamage(ctx);
    }

    /// <summary>
    /// 동물에 데미지를 넣는다. 멀티플레이에서는 호스트에 보고하고, 넉백 방향 계산용으로 내 위치를 함께 보낸다.
    /// </summary>
    private void DamageAnimal(Animal animal, DamageContext ctx, ItemDataSO handItem, ActionType actionType)
    {
#if PHOTON_FUSION
        var director = NetworkAnimalDirector.Instance;
        if (director != null)
        {
            director.ReportDamage(animal, ctx.Amount, transform.position, handItem.itemID, actionType);
            return;
        }
#endif
        // 싱글플레이: 디렉터가 없으므로 그대로 로컬 적용
        animal.ApplyDamage(ctx);
    }

    private bool TryRaycastToolTarget(float range, out RaycastHit hit)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Physics.Raycast(ray, out hit, range, toolUseLayer, QueryTriggerInteraction.Collide);
        }

        Vector3 origin = transform.position + Vector3.up;
        return Physics.Raycast(origin, transform.forward, out hit, range, toolUseLayer, QueryTriggerInteraction.Collide);
    }

    private bool IsPointerOverUI()
    {
        if (Main.Instance == null || Main.Input == null || Mouse.current == null)
            return false;

        return Main.Input.IsPointerOverUI(Mouse.current.position.ReadValue());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}

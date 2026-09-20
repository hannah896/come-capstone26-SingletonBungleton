using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 아이템 보관 기능을 할 수 있는 구조물들의 부모 클래스(상자, 냉장고, 요리솥 등)
/// </summary>
public abstract class StorageStation : StationBase
{
    /// <summary>보관함 UI 제목 등에 쓰는 표시 이름. 하위 클래스가 오버라이드한다.</summary>
    public virtual string DisplayName => StationType.ToString();

    [Header("보관 설정")]
    [SerializeField] private int slotCount = 16;

    [SerializeField] private List<ItemDataSO> slots = new();
    [SerializeField] private List<int> stackCounts = new();     // 각 슬롯에 있는 아이템의 개수 (0이면 빈 슬롯)
    [SerializeField] private List<float> expirationTimestamps = new(); // 슬롯별 소비기한 만료 시각 (0이면 부패 없음)

    [Header("소비기한")]
    [Tooltip("소비기한이 지난 스택이 전환될 아이템 (SpecialType.Rot). 비워두면 이 보관함에서는 부패하지 않는다.")]
    [SerializeField] private ItemDataSO rotItemSO;

    private float nextExpirationCheckTime;
    private const float ExpirationCheckInterval = 1f;

    /// <summary>
    /// 보관 중 소비기한이 느려지는 배수. 1 = 인벤토리와 동일 속도, 3 = 3배 오래 간다(냉장고).
    /// 아이템이 들어올 때 남은 시간에 곱하고, 나갈 때 나눠서 되돌린다.
    /// </summary>
    protected virtual float ExpirationMultiplier => 1f;

    // 보관함 내용이 변경될 때마다 호출되는 이벤트 (UI 등에서 구독하여 업데이트에 활용)
    public event Action OnStorageChanged;

    public IReadOnlyList<ItemDataSO> Slots => slots;
    public IReadOnlyList<int> StackCounts => stackCounts;
    public int SlotCount => slotCount;

    protected override void Awake()
    {
        base.Awake();
        InitializeSlots();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        InitializeSlots();
    }

    public override void Interact(InteractionContext context)
    {
        OnInteract(context);
    }

    /// <summary>보관함은 비어있을 때만 부술 수 있다.</summary>
    public override bool CanDemolish()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && stackCounts[i] > 0)
                return false;

        return true;
    }

    /// <summary>보관함을 연다. 하위 클래스가 base.OnInteract(context)로 그대로 쓰면 된다.</summary>
    protected virtual void OnInteract(InteractionContext context)
    {
        PlayerInventory inventory = context.Instigator != null
            ? context.Instigator.GetComponent<PlayerInventory>()
            : null;
        if (inventory == null) return;

        OpenStoragePopupAsync(inventory).Forget();
    }

    private async UniTask OpenStoragePopupAsync(PlayerInventory inventory)
    {
        UI_Popup_Chest popup = await Extensions.ShowPopup<UI_Popup_Chest>(clickClose: true);
        popup.Bind(this, inventory);
    }

    private void Update()
    {
        if (Time.time < nextExpirationCheckTime) return;
        nextExpirationCheckTime = Time.time + ExpirationCheckInterval;
        CheckExpirations();
    }

    /// <summary>
    /// 해당 슬롯 아이템의 남은 소비기한(초). 보관함 배수를 되돌린 "인벤토리 기준" 값이라
    /// 그대로 다른 보관함이나 인벤토리로 넘기면 된다. 부패하지 않는 아이템이면 -1.
    /// </summary>
    public float GetRemainingSeconds(int index)
    {
        if (index < 0 || index >= expirationTimestamps.Count) return -1f;
        if (expirationTimestamps[index] <= 0f) return -1f;

        float storedRemaining = Mathf.Max(0f, expirationTimestamps[index] - Time.time);
        return storedRemaining / Mathf.Max(0.0001f, ExpirationMultiplier);
    }

    /// <summary>남은 소비기한(초)을 이 보관함의 만료 시각으로 환산한다. remainingSeconds가 음수면 아이템 기본값으로 새로 채운다.</summary>
    private float ToDeadline(ItemDataSO itemData, float remainingSeconds)
    {
        if (itemData == null) return 0f;

        if (remainingSeconds < 0f)
        {
            if (itemData.expirationTime <= 0f) return 0f;
            remainingSeconds = itemData.expirationTime * 60f;
        }

        if (remainingSeconds <= 0f) return 0f;
        return Time.time + remainingSeconds * Mathf.Max(0.0001f, ExpirationMultiplier);
    }

    /// <summary>소비기한이 지난 스택을 rotItemSO로 전환한다.</summary>
    private void CheckExpirations()
    {
        if (rotItemSO == null) return;

        bool anyExpired = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || stackCounts[i] <= 0) continue;
            if (expirationTimestamps[i] <= 0f || Time.time < expirationTimestamps[i]) continue;

            int rotAmount = stackCounts[i];
            ClearSlot(i);
            TryAddItemToSlots(rotItemSO, rotAmount, out _, -1f);
            anyExpired = true;
        }

        if (anyExpired)
            OnStorageChanged?.Invoke();
    }

    // 상자에 아이템을 추가하는 메서드, 남은 개수 반환, 성공 여부 반환
    // remainingSeconds: 넘겨받은 남은 소비기한(초). 음수면 아이템 기본 소비기한으로 새로 시작한다.
    public bool AddItem(ItemDataSO itemData, int amount, out int remainingAmount, float remainingSeconds = -1f)
    {
        bool added = TryAddItemToSlots(itemData, amount, out remainingAmount, remainingSeconds);
        OnStorageChanged?.Invoke();
        return added;
    }
    public bool AddItemAt(ItemDataSO itemData,  int index,  int amount = 1)
    {
        return AddItemAt(itemData, index,  amount, out int remainingAmount) && remainingAmount <= 0;
    }

    public bool AddItemAt(ItemDataSO itemData, int index, int amount, out int remainingAmount, float remainingSeconds = -1f)
    {
        remainingAmount = amount;
        if (index < 0 || index >= slots.Count) return false;
        if (itemData == null || amount <= 0) return false;
        if (slots[index] != null && slots[index] != itemData)
            return false; // 다른 아이템이 있는 슬롯에는 추가 불가
        int maxStack = Mathf.Max(1, itemData.maxStack);
        int currentCount = stackCounts[index];
        int space = maxStack - currentCount;
        if (space <= 0)
            return false; // 스택이 이미 가득 찬 경우
        int addAmount = Mathf.Min(space, remainingAmount);
        bool wasEmpty = currentCount <= 0;
        slots[index] = itemData;
        stackCounts[index] += addAmount;
        remainingAmount -= addAmount;

        // 스택에 합칠 때는 더 이른 기한을 남긴다 (신선한 걸 얹어서 기한을 되살리지 못하게)
        float deadline = ToDeadline(itemData, remainingSeconds);
        expirationTimestamps[index] = wasEmpty || expirationTimestamps[index] <= 0f
            ? deadline
            : (deadline <= 0f ? expirationTimestamps[index] : Mathf.Min(expirationTimestamps[index], deadline));

        OnStorageChanged?.Invoke();
        return true;
    }

    // 슬롯 간 아이템 교환 또는 같은 아이템이면 스택 합치기, 인덱스 유효성 검사 포함
    public void SwapOrMergeSlots(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= slotCount || toIndex < 0 || toIndex >= slotCount) return;
        ItemDataSO tempItem = slots[fromIndex];
        int tempCount = stackCounts[fromIndex];
        float tempDeadline = expirationTimestamps[fromIndex];
        if (slots[fromIndex] == slots[toIndex] && tempItem != null)
        {
            // 같은 아이템이면 합치기 — 기한은 둘 중 더 이른 쪽을 따른다
            int maxStack = Mathf.Max(1, tempItem.maxStack);
            int total = tempCount + stackCounts[toIndex];
            stackCounts[toIndex] = Mathf.Min(total, maxStack);
            stackCounts[fromIndex] = total - stackCounts[toIndex];

            float merged = tempDeadline <= 0f ? expirationTimestamps[toIndex]
                         : expirationTimestamps[toIndex] <= 0f ? tempDeadline
                         : Mathf.Min(tempDeadline, expirationTimestamps[toIndex]);
            expirationTimestamps[toIndex] = merged;

            if (stackCounts[fromIndex] <= 0)
                ClearSlot(fromIndex);
            else
                expirationTimestamps[fromIndex] = merged;
        }
        else
        {
            // 다른 아이템이면 교환
            slots[fromIndex] = slots[toIndex];
            stackCounts[fromIndex] = stackCounts[toIndex];
            expirationTimestamps[fromIndex] = expirationTimestamps[toIndex];
            slots[toIndex] = tempItem;
            stackCounts[toIndex] = tempCount;
            expirationTimestamps[toIndex] = tempDeadline;
        }
        OnStorageChanged?.Invoke();
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
                OnStorageChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    // 슬롯 인덱스를 지정하여 특정 개수 만큼 아이템 제거, 성공 여부 반환
    public bool RemoveItemAt(int index, int amount = 1)
    {
        if (index < 0 || index >= slots.Count) return false;
        if (slots[index] == null || stackCounts[index] < amount) return false;

        stackCounts[index] -= amount;
        if (stackCounts[index] <= 0)
        {
            ClearSlot(index);
        }

        OnStorageChanged?.Invoke();
        return true;
    }

    // 특정 아이템이 보관되어 있는지, 요청된 개수 이상으로 보관되어 있는지 여부 반환
    public bool HasItem(ItemDataSO itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    // 보관된 아이템 개수를 반환, 아이템이 없거나 null이면 0
    public int GetItemCount(ItemDataSO itemData)
    {
        if (itemData == null) return 0;

        int count = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == itemData && stackCounts[i] > 0)
                count += stackCounts[i];

        return count;
    }

    /// <summary>
    /// 멀티 클라 전용: 호스트가 복제한 슬롯 내용으로 덮어쓴다. 실제로 바뀐 칸이 있을 때만 변경 이벤트를 보낸다.
    /// 게임 코드는 이 메서드 대신 StructureSync를 통해 보관함을 조작한다.
    /// </summary>
    public void ApplyNetworkSlots(ItemDataSO[] items, int[] counts)
    {
        if (items == null || counts == null) return;

        bool changed = false;
        int count = Mathf.Min(slots.Count, Mathf.Min(items.Length, counts.Length));
        for (int i = 0; i < count; i++)
        {
            ItemDataSO itemData = counts[i] > 0 ? items[i] : null;
            int stack = itemData != null ? counts[i] : 0;
            if (slots[i] == itemData && stackCounts[i] == stack) continue;

            slots[i] = itemData;
            stackCounts[i] = stack;
            changed = true;
        }

        if (changed)
            OnStorageChanged?.Invoke();
    }

    // 슬롯 개수 변경 메서드, 기존 데이터 유지 또는 초기화 후 이벤트 호출
    public void SetSlotCount(int count)
    {
        slotCount = Mathf.Max(1, count);
        InitializeSlots();
        OnStorageChanged?.Invoke();
    }

    // slotCount 변경 시점과 초기화 시점에 슬롯 리스트와
    // 스택 카운트 리스트의 크기를 slotCount에 맞게 조정하고, 기존 데이터 유지 또는 초기화
    private void InitializeSlots()
    {
        if (slotCount < 1) slotCount = 1;

        while (slots.Count < slotCount)
            slots.Add(null);

        while (stackCounts.Count < slotCount)
            stackCounts.Add(0);

        while (expirationTimestamps.Count < slotCount)
            expirationTimestamps.Add(0f);

        while (slots.Count > slotCount)
            slots.RemoveAt(slots.Count - 1);

        while (stackCounts.Count > slotCount)
            stackCounts.RemoveAt(stackCounts.Count - 1);

        while (expirationTimestamps.Count > slotCount)
            expirationTimestamps.RemoveAt(expirationTimestamps.Count - 1);

        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null)
            {
                stackCounts[i] = 0;
                continue;
            }

            if (stackCounts[i] <= 0)
                stackCounts[i] = 1;
        }
    }

    // 이미 보관된 아이템과 같은 종류의 아이템이 있다면 먼저 그 스택을 채우는 시도,
    // 상자가 꽉 찬 경우 남은 개수 반환
    private void FillExistingStacks(ItemDataSO itemData, ref int remainingAmount, float deadline)
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

            // 합쳐지는 스택은 더 이른 기한을 따른다
            if (deadline > 0f)
                expirationTimestamps[i] = expirationTimestamps[i] > 0f
                    ? Mathf.Min(expirationTimestamps[i], deadline)
                    : deadline;
        }
    }

    // 가장 먼저 비어있는 슬롯부터 아이템을 추가하는 시도, 성공 여부와 남은 개수 반환
    private bool TryAddItemToSlots(ItemDataSO itemData, int amount, out int remainingAmount, float remainingSeconds = -1f)
    {
        remainingAmount = amount;
        if (itemData == null || amount <= 0) return false;

        float deadline = ToDeadline(itemData, remainingSeconds);

        if (itemData.isStackable)
            FillExistingStacks(itemData, ref remainingAmount, deadline);

        // remainingAmount : 요청이 들어온 아이템 개수 중 아직 보관되지 않은 개수
        while (remainingAmount > 0)
        {
            // 빈 슬롯 찾기 (없으면 실패)
            int emptyIndex = GetEmptySlotIndex();
            if (emptyIndex < 0)
                return false;

            int stackSize = itemData.isStackable
                ? Mathf.Min(remainingAmount, Mathf.Max(1, itemData.maxStack))
                : 1;

            slots[emptyIndex] = itemData;
            stackCounts[emptyIndex] = stackSize;
            expirationTimestamps[emptyIndex] = deadline;
            remainingAmount -= stackSize;
        }

        return true;
    }

    // 비어있는 슬롯의 인덱스를 반환, 없으면 -1
    private int GetEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                return i;

        return -1;
    }

    // 슬롯 초기화 또는 아이템이 제거되어 빈 슬롯이 된 경우 호출
    private void ClearSlot(int index)
    {
        slots[index] = null;
        stackCounts[index] = 0;
        if (index < expirationTimestamps.Count)
            expirationTimestamps[index] = 0f;
    }
}

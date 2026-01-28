using UnityEngine;

/// 게임 내에 실제로 존재하는 아이템 인스턴스 (데이터 실체)

[System.Serializable]
public class Item
{
    // === 데이터 및 상태 ===
    public ItemDataSO itemData;        // 원본 데이터(설계도)
    public int stackCount;             // 현재 수량
    public int currentDurability;      // 현재 내구도 (도구/무기)
    public float currentFreshness;     // 현재 신선도 (음식)
    private float _createdTime;        // 생성 시점 (신선도 계산용)

    /// 아이템 생성 및 초기화
    public Item(ItemDataSO data, int count = 1)
    {
        itemData = data;
        stackCount = Mathf.Min(count, data.maxStack);
        currentDurability = data.maxDurability;

        if (data.hasFreshness)
        {
            currentFreshness = data.maxFreshness;
            _createdTime = Time.time;
        }
    }

    // === 음식 및 신선도 로직 ===


    /// 경과 시간에 따른 신선도 갱신 (인벤토리에서 호출)
    public void UpdateFreshness()
    {
        if (!itemData.hasFreshness) return;

        float elapsedTime = Time.time - _createdTime;
        currentFreshness = Mathf.Max(0, itemData.maxFreshness - elapsedTime);

        if (currentFreshness <= 0)
            Debug.Log($"[Item] {itemData.itemName}이(가) 부패했습니다.");
    }

    public float GetFreshnessPercent() => itemData.hasFreshness ? (currentFreshness / itemData.maxFreshness) * 100f : 100f;


    /// 아이템 소비 (음식 타입 전용)
    public bool Consume()
    {
        if (itemData.itemType != ItemType.Food || stackCount <= 0) return false;
        if (itemData.hasFreshness && currentFreshness <= 0) return false;

        stackCount--;
        ApplySurvivalEffects();
        return true;
    }

    private void ApplySurvivalEffects()
    {
        // TODO: Survival Manager 연동
        if (itemData.hungerRestore > 0) Debug.Log($"배고픔 +{itemData.hungerRestore}");
        if (itemData.healthRestore > 0) Debug.Log($"체력 +{itemData.healthRestore}");
        if (itemData.sanityRestore > 0) Debug.Log($"정신력 +{itemData.sanityRestore}");
    }

    // === 스택 및 수량 관리 ===

    /// 대상 아이템과 겹치기가 가능한지 확인
    public bool CanStackWith(Item other)
    {
        if (other == null || other.itemData != this.itemData) return false;
        if (!itemData.isStackable || stackCount >= itemData.maxStack) return false;

        // 음식은 신선도 차이가 적을 때만 합침 (예: 5분 이내)
        if (itemData.hasFreshness && Mathf.Abs(currentFreshness - other.currentFreshness) > 300f)
            return false;

        return true;
    }

    /// 수량 추가 후 남은 수량 반환
    public int AddStack(int amount)
    {
        int space = itemData.maxStack - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;
    }

    /// 수량 제거 후 실제 제거된 수량 반환
    public int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }

    // === 내구도 및 유효성 ===

    public void UseDurability(int amount = 1)
    {
        if (!itemData.hasDurability) return;

        currentDurability = Mathf.Max(0, currentDurability - amount);
        if (currentDurability <= 0) Debug.Log($"{itemData.itemName} 파손됨.");
    }

    public float GetDurabilityPercent() => itemData.hasDurability ? ((float)currentDurability / itemData.maxDurability) * 100f : 100f;

    public bool IsUsable()
    {
        if (itemData.hasDurability && currentDurability <= 0) return false;
        if (itemData.hasFreshness && currentFreshness <= 0) return false;
        return stackCount > 0;
    }

    /// 아이템 복제 (분할 기능 등에 사용)
    public Item Clone(int count)
    {
        Item newItem = new Item(itemData, count)
        {
            currentDurability = this.currentDurability,
            currentFreshness = this.currentFreshness,
            _createdTime = this._createdTime
        };
        return newItem;
    }

    public override string ToString()
    {
        string info = $"{itemData.itemName} x{stackCount}";
        if (itemData.hasDurability) info += $" [내구도: {GetDurabilityPercent():F0}%]";
        if (itemData.hasFreshness) info += $" [신선도: {GetFreshnessPercent():F0}%]";
        return info;
    }
}
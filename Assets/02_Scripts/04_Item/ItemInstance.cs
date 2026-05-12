using UnityEngine;

[System.Serializable]
public class ItemInstance
{
    public ItemDataSO data;
    public int stackCount = 1;
    public float currentDurability;
    public float freshness = 1f;

    public ItemInstance() { }

    public ItemInstance(ItemDataSO data, int count = 1)
    {
        this.data = data;
        stackCount = Mathf.Max(1, count);
        currentDurability = data.hasDurability ? data.maxDurability : 0f;
        freshness = (data.itemType == ItemType.Food) ? 1f : 0f;
    }

    public bool CanStackWith(ItemInstance other)
    {
        if (other == null || other.data != data) return false;
        if (!data.isStackable) return false;
        return stackCount < data.maxStack;
    }

    // 반환값: 못 담은 초과분
    public int AddStack(int amount)
    {
        int space = data.maxStack - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;
    }

    // 반환값: 실제 제거된 수량
    public int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }

    public float GetDurabilityPercent()
    {
        if (!data.hasDurability || data.maxDurability <= 0f) return 1f;
        return currentDurability / data.maxDurability;
    }

    public bool IsExpired() => data.itemType == ItemType.Food && freshness <= 0f;

    public override string ToString()
    {
        string s = $"{data.itemName} x{stackCount}";
        if (data.hasDurability) s += $" [내구도:{currentDurability:F0}/{data.maxDurability:F0}]";
        if (data.itemType == ItemType.Food) s += $" [신선도:{freshness * 100f:F0}%]";
        return s;
    }
}

using System;
using UnityEngine;

public class CookingPot : StorageStation
{
    private const int CookingSlotCount = 6;

    public override StationType StationType => StationType.CookingPot;

    [Header("요리 상태")]
    [SerializeField] private bool isCooking;

    public bool IsCooking => isCooking;

    public event Action<bool> OnCookingStateChanged;

    protected override void Awake()
    {
        base.Awake();
        SetSlotCount(CookingSlotCount);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        SetSlotCount(CookingSlotCount);
    }

    // 요리 하기 버튼 활성화 조건 : 슬롯에 아이템이 4개 이상 있고, 그 중 최소 하나가 음식이어야 함
    public bool CanCook()
    {
        int totalItemCount = 0;
        bool hasFood = false;

        for (int i = 0; i < Slots.Count; i++)
        {
            ItemDataSO itemData = Slots[i];
            if (itemData == null) continue;

            int count = StackCounts[i];
            if (count <= 0) continue;

            totalItemCount += count;
            if (itemData.itemType == ItemType.Food)
                hasFood = true;
        }

        return totalItemCount >= 4 && hasFood;
    }

    public bool TryStartCooking()
    {
        if (isCooking) return false;
        if (!CanCook()) return false;

        isCooking = true;
        OnCookingStateChanged?.Invoke(true);
        return true;
    }

    public void StopCooking()
    {
        if (!isCooking) return;

        isCooking = false;
        OnCookingStateChanged?.Invoke(false);
    }
}

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CookingPot : StorageStation
{
    private const int CookingSlotCount = 6;

    public override StationType StationType => StationType.CookingPot;
    public override string DisplayName => "화덕";

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

    /// <summary>멀티 클라 전용: 호스트가 복제한 요리 상태를 반영한다. (게임 코드는 StructureSync.StartCooking/StopCooking 사용)</summary>
    public void ApplyNetworkCooking(bool cooking)
    {
        if (isCooking == cooking) return;

        isCooking = cooking;
        OnCookingStateChanged?.Invoke(cooking);
    }

    /// <summary>
    /// E키 상호작용: 상자처럼 슬롯을 여는 대신, 크래프팅 UI의 "요리" 카테고리를 그대로 옮긴
    /// 전용 레시피 팝업을 연다. (재료는 인벤토리에서 직접 소모 — 이 클래스의 슬롯 저장 기능은 쓰지 않는다)
    /// </summary>
    protected override void OnInteract(InteractionContext context)
    {
        PlayerInventory inventory = context.Instigator != null
            ? context.Instigator.GetComponent<PlayerInventory>()
            : null;
        if (inventory == null) return;

        OpenCookingPopupAsync(inventory).Forget();
    }

    private async UniTask OpenCookingPopupAsync(PlayerInventory inventory)
    {
        UI_Popup_CookingPot popup = await Extensions.ShowPopup<UI_Popup_CookingPot>(clickClose: true);
        popup.Bind(inventory);
    }
}

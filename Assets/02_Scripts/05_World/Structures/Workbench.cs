using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 작업대. 화덕과 동일한 방식으로, E키로 열면 무기/방어구 제작 전용 팝업이 뜬다.
/// (CraftingStation의 기본 CanInteract=false를 여기서만 뒤집는다)
/// </summary>
public class Workbench : CraftingStation
{
    public override StationType StationType => StationType.Workbench;

    public override bool CanInteract(InteractionContext ctx) => true;

    public override void Interact(InteractionContext context)
    {
        PlayerInventory inventory = context.Instigator != null
            ? context.Instigator.GetComponent<PlayerInventory>()
            : null;
        if (inventory == null) return;

        OpenWorkbenchPopupAsync(inventory).Forget();
    }

    private async UniTask OpenWorkbenchPopupAsync(PlayerInventory inventory)
    {
        UI_Popup_Workbench popup = await Extensions.ShowPopup<UI_Popup_Workbench>(clickClose: true);
        popup.Bind(inventory);
    }
}

using Cysharp.Threading.Tasks;

/// <summary>
/// 문(달) 연성대 (2단계 작업대). 작업대와 같은 방식으로, E키로 열면 마력 무기 제작 전용 팝업이 뜬다.
/// </summary>
public class LunarForge : CraftingStation
{
    public override StationType StationType => StationType.LunarForge;

    public override bool CanInteract(InteractionContext ctx) => true;

    public override void Interact(InteractionContext context)
    {
        PlayerInventory inventory = context.Instigator != null
            ? context.Instigator.GetComponent<PlayerInventory>()
            : null;
        if (inventory == null) return;

        OpenPopupAsync(inventory).Forget();
    }

    private async UniTask OpenPopupAsync(PlayerInventory inventory)
    {
        UI_Popup_LunarForge popup = await Extensions.ShowPopup<UI_Popup_LunarForge>(clickClose: true);
        popup.Bind(inventory);
    }
}
